import type { FastifyInstance } from 'fastify';
import { db } from '../db.js';
import { requireAdmin } from '../auth.js';

export default async function ticketRoutes(app: FastifyInstance) {
  // --- Public: the family help website submits a ticket here ----------------
  // No admin login. (In production, put a simple per-family sign-in in front.)
  app.post('/public/tickets', async (req, reply) => {
    const body = (req.body ?? {}) as {
      owner_id?: number;
      from_name?: string;
      topic?: string;
      device_hint?: string;
      body?: string;
      urgent?: boolean;
    };
    if (!body.body && !body.topic) {
      return reply.code(400).send({ error: 'Tell us a little about the problem' });
    }
    const info = db
      .prepare(
        `INSERT INTO tickets (owner_id, from_name, topic, device_hint, body, urgent)
         VALUES (?, ?, ?, ?, ?, ?)`,
      )
      .run(
        body.owner_id ?? null,
        body.from_name ?? '',
        body.topic ?? 'other',
        body.device_hint ?? '',
        body.body ?? '',
        body.urgent ? 1 : 0,
      );
    return reply.send({ id: info.lastInsertRowid, status: 'open' });
  });

  // --- Admin: inbox ---------------------------------------------------------
  app.register(async (secured) => {
    secured.addHook('preHandler', requireAdmin);

    secured.get('/tickets', async (req) => {
      const { status } = (req.query ?? {}) as { status?: string };
      const where = status ? `WHERE t.status = ?` : '';
      const rows = db
        .prepare(
          `SELECT t.*, o.name AS owner_name FROM tickets t
           LEFT JOIN owners o ON o.id = t.owner_id ${where}
           ORDER BY t.urgent DESC, t.created_at DESC`,
        )
        .all(...(status ? [status] : []));
      return rows;
    });

    secured.get('/tickets/:id', async (req, reply) => {
      const { id } = req.params as { id: string };
      const ticket = db
        .prepare(
          `SELECT t.*, o.name AS owner_name FROM tickets t
           LEFT JOIN owners o ON o.id = t.owner_id WHERE t.id = ?`,
        )
        .get(id);
      if (!ticket) return reply.code(404).send({ error: 'Ticket not found' });
      const messages = db
        .prepare(`SELECT author, body, created_at FROM ticket_messages WHERE ticket_id = ? ORDER BY created_at`)
        .all(id);
      return reply.send({ ticket, messages });
    });

    secured.post('/tickets/:id/reply', async (req, reply) => {
      const { id } = req.params as { id: string };
      const { body } = (req.body ?? {}) as { body?: string };
      if (!body) return reply.code(400).send({ error: 'Reply cannot be empty' });
      const exists = db.prepare(`SELECT id FROM tickets WHERE id = ?`).get(id);
      if (!exists) return reply.code(404).send({ error: 'Ticket not found' });
      db.prepare(`INSERT INTO ticket_messages (ticket_id, author, body) VALUES (?, 'admin', ?)`).run(id, body);
      db.prepare(`UPDATE tickets SET updated_at = datetime('now') WHERE id = ?`).run(id);
      return reply.send({ ok: true });
    });

    secured.patch('/tickets/:id', async (req, reply) => {
      const { id } = req.params as { id: string };
      const { status } = (req.body ?? {}) as { status?: string };
      if (!status || !['open', 'waiting', 'done'].includes(status)) {
        return reply.code(400).send({ error: 'Invalid status' });
      }
      db.prepare(`UPDATE tickets SET status = ?, updated_at = datetime('now') WHERE id = ?`).run(status, id);
      return reply.send({ ok: true });
    });
  });
}
