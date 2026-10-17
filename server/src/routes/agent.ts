import type { FastifyInstance } from 'fastify';
import { randomUUID } from 'node:crypto';
import { db } from '../db.js';
import { newToken, sha256 } from '../crypto.js';
import { authenticateDevice } from '../auth.js';

// Routes the Windows (and later Linux) agent calls. No admin JWT here —
// the agent authenticates with its own device token.
export default async function agentRoutes(app: FastifyInstance) {
  // --- Enroll ---------------------------------------------------------------
  // The agent is installed with a one-time enrollment code. It trades that
  // code for a permanent device id + secret, which it stores locally.
  app.post('/enroll', async (req, reply) => {
    const body = (req.body ?? {}) as {
      token?: string;
      name?: string;
      platform?: string;
      os_version?: string;
      agent_version?: string;
    };
    if (!body.token) return reply.code(400).send({ error: 'Missing enrollment token' });

    const tok = db
      .prepare(
        `SELECT id, owner_id, expires_at, used_at FROM enrollment_tokens WHERE token = ?`,
      )
      .get(body.token) as
      | { id: number; owner_id: number | null; expires_at: string; used_at: string | null }
      | undefined;

    if (!tok) return reply.code(400).send({ error: 'Unknown enrollment token' });
    if (tok.used_at) return reply.code(400).send({ error: 'Enrollment token already used' });
    if (new Date(tok.expires_at.replace(' ', 'T') + 'Z').getTime() < Date.now()) {
      return reply.code(400).send({ error: 'Enrollment token expired' });
    }

    const deviceId = randomUUID();
    const secret = newToken();
    const now = new Date().toISOString();

    const tx = db.transaction(() => {
      db.prepare(
        `INSERT INTO devices (id, auth_token_hash, name, owner_id, platform, os_version, agent_version, last_seen_at, enrolled_at)
         VALUES (?, ?, ?, ?, ?, ?, ?, ?, datetime('now'))`,
      ).run(
        deviceId,
        sha256(secret),
        body.name ?? '',
        tok.owner_id,
        body.platform ?? 'windows',
        body.os_version ?? '',
        body.agent_version ?? '',
        now,
      );
      db.prepare(`UPDATE enrollment_tokens SET used_at = datetime('now') WHERE id = ?`).run(tok.id);
    });
    tx();

    // The secret is shown exactly once.
    return reply.send({ device_id: deviceId, device_token: `${deviceId}.${secret}` });
  });

  // --- Check-in -------------------------------------------------------------
  // Called on a schedule. Carries a health snapshot and optional log events,
  // and returns any pending commands for the agent to run.
  app.post('/checkin', async (req, reply) => {
    const device = authenticateDevice(req);
    if (!device) return reply.code(401).send({ error: 'Bad device token' });

    const body = (req.body ?? {}) as {
      os_version?: string;
      agent_version?: string;
      health?: Record<string, unknown>;
      logs?: Array<{
        occurred_at?: string;
        level?: string;
        source?: string;
        event_id?: number;
        message?: string;
      }>;
    };

    const h = body.health ?? {};
    const num = (k: string) => (typeof h[k] === 'number' ? (h[k] as number) : null);
    const int = (k: string) => (typeof h[k] === 'number' ? Math.round(h[k] as number) : null);

    const tx = db.transaction(() => {
      db.prepare(
        `UPDATE devices SET last_seen_at = datetime('now'),
           os_version = COALESCE(NULLIF(?, ''), os_version),
           agent_version = COALESCE(NULLIF(?, ''), agent_version)
         WHERE id = ?`,
      ).run(body.os_version ?? '', body.agent_version ?? '', device.id);

      db.prepare(
        `INSERT INTO health_reports
          (device_id, os_version, uptime_sec, cpu_percent, mem_percent,
           disk_total_gb, disk_free_gb, battery_percent, battery_charging,
           updates_pending, updates_security, antivirus_ok, raw_json)
         VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
      ).run(
        device.id,
        body.os_version ?? null,
        int('uptime_sec'),
        num('cpu_percent'),
        num('mem_percent'),
        num('disk_total_gb'),
        num('disk_free_gb'),
        int('battery_percent'),
        typeof h['battery_charging'] === 'boolean' ? (h['battery_charging'] ? 1 : 0) : null,
        int('updates_pending') ?? 0,
        int('updates_security') ?? 0,
        typeof h['antivirus_ok'] === 'boolean' ? (h['antivirus_ok'] ? 1 : 0) : null,
        JSON.stringify(h),
      );

      for (const ev of body.logs ?? []) {
        db.prepare(
          `INSERT INTO log_events (device_id, occurred_at, level, source, event_id, message)
           VALUES (?, ?, ?, ?, ?, ?)`,
        ).run(
          device.id,
          ev.occurred_at ?? new Date().toISOString(),
          ev.level ?? 'error',
          ev.source ?? '',
          ev.event_id ?? null,
          ev.message ?? '',
        );
      }
    });
    tx();

    // Hand back pending commands and mark them sent.
    const pending = db
      .prepare(
        `SELECT id, type, payload_json FROM commands WHERE device_id = ? AND status = 'pending' ORDER BY created_at`,
      )
      .all(device.id) as Array<{ id: string; type: string; payload_json: string }>;

    if (pending.length) {
      const mark = db.prepare(`UPDATE commands SET status = 'sent', sent_at = datetime('now') WHERE id = ?`);
      const markAll = db.transaction((ids: string[]) => ids.forEach((id) => mark.run(id)));
      markAll(pending.map((c) => c.id));
    }

    return reply.send({
      commands: pending.map((c) => ({ id: c.id, type: c.type, payload: JSON.parse(c.payload_json) })),
      // The agent may use this to pace itself.
      checkin_interval_sec: 300,
    });
  });

  // --- Command result -------------------------------------------------------
  app.post('/commands/:id/result', async (req, reply) => {
    const device = authenticateDevice(req);
    if (!device) return reply.code(401).send({ error: 'Bad device token' });

    const { id } = req.params as { id: string };
    const body = (req.body ?? {}) as { status?: string; result?: unknown };
    const status = body.status === 'done' || body.status === 'failed' ? body.status : 'done';

    const cmd = db
      .prepare(`SELECT id FROM commands WHERE id = ? AND device_id = ?`)
      .get(id, device.id) as { id: string } | undefined;
    if (!cmd) return reply.code(404).send({ error: 'Unknown command' });

    db.prepare(
      `UPDATE commands SET status = ?, result_json = ?, completed_at = datetime('now') WHERE id = ?`,
    ).run(status, JSON.stringify(body.result ?? null), id);

    return reply.send({ ok: true });
  });
}
