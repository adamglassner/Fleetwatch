import Fastify from 'fastify';
import cors from '@fastify/cors';
import jwt from '@fastify/jwt';
import { config } from './config.js';
import './db.js'; // applies schema on import
import agentRoutes from './routes/agent.js';
import adminRoutes from './routes/admin.js';
import ticketRoutes from './routes/tickets.js';

const app = Fastify({ logger: true });

await app.register(cors, { origin: config.corsOrigin === '*' ? true : config.corsOrigin.split(',') });
await app.register(jwt, { secret: config.jwtSecret, sign: { expiresIn: config.jwtExpiresIn } });

app.get('/health', async () => ({ ok: true, service: 'fleetwatch-server', time: new Date().toISOString() }));

await app.register(agentRoutes, { prefix: '/api/agent' });
await app.register(adminRoutes, { prefix: '/api' });
await app.register(ticketRoutes, { prefix: '/api' });

try {
  await app.listen({ port: config.port, host: '0.0.0.0' });
} catch (err) {
  app.log.error(err);
  process.exit(1);
}
