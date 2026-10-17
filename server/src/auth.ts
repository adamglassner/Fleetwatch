import type { FastifyReply, FastifyRequest } from 'fastify';
import { db } from './db.js';
import { sha256 } from './crypto.js';

// Admin auth: verifies the JWT set by @fastify/jwt. Use as a preHandler.
export async function requireAdmin(req: FastifyRequest, reply: FastifyReply) {
  try {
    await req.jwtVerify();
  } catch {
    return reply.code(401).send({ error: 'Not signed in' });
  }
}

export interface DeviceRow {
  id: string;
  name: string;
  owner_id: number | null;
  platform: string;
}

// Device auth: the agent sends "Authorization: Bearer <deviceId>.<secret>".
// We look up the device and compare the hashed secret.
export function authenticateDevice(req: FastifyRequest): DeviceRow | null {
  const header = req.headers['authorization'];
  if (!header || !header.startsWith('Bearer ')) return null;
  const raw = header.slice('Bearer '.length).trim();
  const dot = raw.indexOf('.');
  if (dot < 0) return null;
  const deviceId = raw.slice(0, dot);
  const secret = raw.slice(dot + 1);
  if (!deviceId || !secret) return null;

  const row = db
    .prepare('SELECT id, name, owner_id, platform, auth_token_hash FROM devices WHERE id = ?')
    .get(deviceId) as (DeviceRow & { auth_token_hash: string }) | undefined;
  if (!row) return null;
  if (sha256(secret) !== row.auth_token_hash) return null;

  const { auth_token_hash, ...device } = row;
  return device;
}
