# Fleetwatch server

The Linux server that agents check in to, and that the admin dashboard and
family help website talk to. Node + TypeScript + Fastify, with SQLite for
storage (one file, no separate database to run).

## Requirements

- Node.js 20+ (22 recommended)
- That's it. SQLite is embedded.

## Run it (development)

```bash
cd server
cp .env.example .env          # then edit .env — at least set JWT_SECRET
npm install
npm run seed:admin -- you@example.com "a-strong-password" "Adam"
npm run dev                   # http://localhost:8080/health
```

## Run it (production, systemd)

```bash
cd server
npm ci
npm run build
npm run seed:admin -- you@example.com "a-strong-password" "Adam"
# then install the unit file in deploy/fleetwatch-server.service
```

See the comments at the top of `deploy/fleetwatch-server.service`.

## Run it (Docker)

From the repo root:

```bash
echo "JWT_SECRET=$(openssl rand -hex 32)" > .env
docker compose up -d --build
# create your admin login inside the container:
docker compose exec server node dist/index.js >/dev/null 2>&1 &  # ensure DB exists
docker compose exec server sh -c 'node --import tsx src/scripts/create-admin.ts you@example.com "a-strong-password" "Adam"'
```

> For a real deployment put this behind a reverse proxy (Caddy, nginx,
> Traefik) that terminates HTTPS. Agents should only ever talk to the server
> over HTTPS.

## Configuration

All settings come from environment variables (see `.env.example`):

| Variable | Meaning |
|---|---|
| `PORT` | Port to listen on (default 8080) |
| `DATABASE_PATH` | Where the SQLite file lives |
| `JWT_SECRET` | Secret for admin login tokens — **required in production** |
| `JWT_EXPIRES_IN` | Admin session length (e.g. `7d`) |
| `CORS_ORIGIN` | Allowed browser origin(s), comma-separated, or `*` for testing |

## API overview

### Agent (device token auth: `Authorization: Bearer <deviceId>.<secret>`)

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/agent/enroll` | Trade a one-time code for a device id + token |
| POST | `/api/agent/checkin` | Send health + logs, receive pending commands |
| POST | `/api/agent/commands/:id/result` | Report a command's outcome |

### Admin (JWT auth from `/api/auth/login`)

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/auth/login` | Sign in, get a token |
| GET | `/api/devices` | List devices with latest health |
| GET | `/api/devices/:id` | Device detail: health, logs, commands |
| PATCH | `/api/devices/:id` | Rename / reassign owner |
| POST | `/api/devices/:id/commands` | Queue a command (install_app, install_updates, restart, …) |
| GET/POST | `/api/owners` | List / add family members |
| POST | `/api/enrollment-tokens` | Create a one-time enrollment code |
| GET | `/api/tickets` | Help-request inbox |
| GET | `/api/tickets/:id` | One ticket with replies |
| POST | `/api/tickets/:id/reply` | Reply to a ticket |
| PATCH | `/api/tickets/:id` | Change ticket status |

### Public (family help website)

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/public/tickets` | Submit a help request |

## Enrolling a device (end to end)

1. Admin signs in, creates an owner (e.g. "Mom"), then creates an enrollment
   code for that owner (`POST /api/enrollment-tokens`).
2. The code goes into the Windows installer (or `appsettings.json`).
3. On first run the agent calls `/api/agent/enroll` and stores the returned
   device token locally.
4. From then on it calls `/api/agent/checkin` on a schedule.
