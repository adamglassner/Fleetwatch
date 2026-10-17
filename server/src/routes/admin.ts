import type { FastifyInstance } from 'fastify';
import { randomUUID } from 'node:crypto';
import { db } from '../db.js';
import { verifyPassword, newToken } from '../crypto.js';
import { requireAdmin } from '../auth.js';

export default async function adminRoutes(app: FastifyInstance) {
  // --- Login ----------------------------------------------------------------
  app.post('/auth/login', async (req, reply) => {
    const { email, password } = (req.body ?? {}) as { email?: string; password?: string };
    if (!email || !password) return reply.code(400).send({ error: 'Email and password required' });

    const user = db
      .prepare(`SELECT id, email, display_name, password_hash, role FROM users WHERE email = ?`)
      .get(email) as
      | { id: number; email: string; display_name: string; password_hash: string; role: string }
      | undefined;

    if (!user || !verifyPassword(password, user.password_hash)) {
      return reply.code(401).send({ error: 'Wrong email or password' });
    }

    const token = app.jwt.sign({ sub: user.id, email: user.email, role: user.role });
    return reply.send({
      token,
      user: { id: user.id, email: user.email, display_name: user.display_name, role: user.role },
    });
  });

  // Everything below requires a signed-in admin.
  app.register(async (secured) => {
    secured.addHook('preHandler', requireAdmin);

    // --- Owners (family members) -------------------------------------------
    secured.get('/owners', async () => {
      return db.prepare(`SELECT id, name, home, created_at FROM owners ORDER BY name`).all();
    });

    secured.post('/owners', async (req, reply) => {
      const { name, home } = (req.body ?? {}) as { name?: string; home?: string };
      if (!name) return reply.code(400).send({ error: 'Name required' });
      const info = db.prepare(`INSERT INTO owners (name, home) VALUES (?, ?)`).run(name, home ?? '');
      return reply.send({ id: info.lastInsertRowid, name, home: home ?? '' });
    });

    // --- Enrollment codes ---------------------------------------------------
    secured.post('/enrollment-tokens', async (req, reply) => {
      const { owner_id, label, ttl_hours } = (req.body ?? {}) as {
        owner_id?: number;
        label?: string;
        ttl_hours?: number;
      };
      const token = newToken(9); // short-ish, still unguessable
      const hours = ttl_hours && ttl_hours > 0 ? ttl_hours : 72;
      const expires = new Date(Date.now() + hours * 3600_000).toISOString().replace('T', ' ').slice(0, 19);
      const info = db
        .prepare(
          `INSERT INTO enrollment_tokens (token, owner_id, label, expires_at) VALUES (?, ?, ?, ?)`,
        )
        .run(token, owner_id ?? null, label ?? '', expires);
      return reply.send({ id: info.lastInsertRowid, token, expires_at: expires });
    });

    // --- Devices ------------------------------------------------------------
    secured.get('/devices', async () => {
      // Each device with its owner and latest health snapshot.
      return db
        .prepare(
          `SELECT d.id, d.name, d.platform, d.os_version, d.agent_version,
                  d.last_seen_at, d.enrolled_at,
                  o.name AS owner_name, o.home AS owner_home,
                  h.updates_pending, h.updates_security, h.disk_total_gb, h.disk_free_gb,
                  h.battery_percent, h.battery_charging, h.antivirus_ok, h.reported_at
           FROM devices d
           LEFT JOIN owners o ON o.id = d.owner_id
           LEFT JOIN health_reports h ON h.id = (
             SELECT id FROM health_reports WHERE device_id = d.id ORDER BY reported_at DESC LIMIT 1
           )
           ORDER BY o.name, d.name`,
        )
        .all();
    });

    secured.get('/devices/:id', async (req, reply) => {
      const { id } = req.params as { id: string };
      const device = db
        .prepare(
          `SELECT d.*, o.name AS owner_name, o.home AS owner_home
           FROM devices d LEFT JOIN owners o ON o.id = d.owner_id WHERE d.id = ?`,
        )
        .get(id);
      if (!device) return reply.code(404).send({ error: 'Device not found' });

      const health = db
        .prepare(`SELECT * FROM health_reports WHERE device_id = ? ORDER BY reported_at DESC LIMIT 1`)
        .get(id);
      const logs = db
        .prepare(
          `SELECT occurred_at, level, source, event_id, message FROM log_events
           WHERE device_id = ? ORDER BY occurred_at DESC LIMIT 50`,
        )
        .all(id);
      const commands = db
        .prepare(
          `SELECT id, type, status, created_at, completed_at, result_json FROM commands
           WHERE device_id = ? ORDER BY created_at DESC LIMIT 25`,
        )
        .all(id);

      return reply.send({ device, health, logs, commands });
    });

    secured.patch('/devices/:id', async (req, reply) => {
      const { id } = req.params as { id: string };
      const { name, owner_id } = (req.body ?? {}) as { name?: string; owner_id?: number | null };
      const existing = db.prepare(`SELECT id FROM devices WHERE id = ?`).get(id);
      if (!existing) return reply.code(404).send({ error: 'Device not found' });
      db.prepare(
        `UPDATE devices SET name = COALESCE(?, name), owner_id = COALESCE(?, owner_id) WHERE id = ?`,
      ).run(name ?? null, owner_id ?? null, id);
      return reply.send({ ok: true });
    });

    // --- Commands -----------------------------------------------------------
    const ALLOWED_COMMANDS = new Set([
      'install_app',
      'install_updates',
      'restart',
      'run_speedtest',
      'scan_network',
    ]);

    secured.post('/devices/:id/commands', async (req, reply) => {
      const { id } = req.params as { id: string };
      const { type, payload } = (req.body ?? {}) as { type?: string; payload?: unknown };
      if (!type || !ALLOWED_COMMANDS.has(type)) {
        return reply.code(400).send({ error: 'Unknown command type' });
      }
      const device = db.prepare(`SELECT id FROM devices WHERE id = ?`).get(id);
      if (!device) return reply.code(404).send({ error: 'Device not found' });

      const user = req.user as { sub: number };
      const cmdId = randomUUID();
      db.prepare(
        `INSERT INTO commands (id, device_id, type, payload_json, created_by) VALUES (?, ?, ?, ?, ?)`,
      ).run(cmdId, id, type, JSON.stringify(payload ?? {}), user.sub);
      return reply.send({ id: cmdId, status: 'pending' });
    });
  });
}
