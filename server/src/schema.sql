-- Fleetwatch database schema (SQLite)
-- Applied automatically on startup; safe to run repeatedly.

PRAGMA journal_mode = WAL;
PRAGMA foreign_keys = ON;

-- Admin users (you, and anyone you let help).
CREATE TABLE IF NOT EXISTS users (
  id            INTEGER PRIMARY KEY AUTOINCREMENT,
  email         TEXT NOT NULL UNIQUE,
  display_name  TEXT NOT NULL DEFAULT '',
  password_hash TEXT NOT NULL,          -- scrypt: salt:hash (hex)
  role          TEXT NOT NULL DEFAULT 'admin',
  created_at    TEXT NOT NULL DEFAULT (datetime('now'))
);

-- People whose devices you look after (Mom, Dad, Grandma...).
CREATE TABLE IF NOT EXISTS owners (
  id          INTEGER PRIMARY KEY AUTOINCREMENT,
  name        TEXT NOT NULL,
  home        TEXT NOT NULL DEFAULT '',  -- e.g. "Mom & Dad's house"
  created_at  TEXT NOT NULL DEFAULT (datetime('now'))
);

-- One-time codes used to enroll a new device.
CREATE TABLE IF NOT EXISTS enrollment_tokens (
  id          INTEGER PRIMARY KEY AUTOINCREMENT,
  token       TEXT NOT NULL UNIQUE,
  owner_id    INTEGER REFERENCES owners(id) ON DELETE SET NULL,
  label       TEXT NOT NULL DEFAULT '',   -- hint for what this code is for
  expires_at  TEXT NOT NULL,
  used_at     TEXT,                        -- NULL until redeemed
  created_at  TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Enrolled devices (computers, and later phones/tablets).
CREATE TABLE IF NOT EXISTS devices (
  id             TEXT PRIMARY KEY,          -- uuid chosen at enroll time
  auth_token_hash TEXT NOT NULL,            -- sha256 of the device's secret token
  name           TEXT NOT NULL DEFAULT '',
  owner_id       INTEGER REFERENCES owners(id) ON DELETE SET NULL,
  platform       TEXT NOT NULL DEFAULT 'windows',  -- windows | linux | ...
  os_version     TEXT NOT NULL DEFAULT '',
  agent_version  TEXT NOT NULL DEFAULT '',
  last_seen_at   TEXT,
  enrolled_at    TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Latest + historical health snapshots reported by the agent.
CREATE TABLE IF NOT EXISTS health_reports (
  id            INTEGER PRIMARY KEY AUTOINCREMENT,
  device_id     TEXT NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
  reported_at   TEXT NOT NULL DEFAULT (datetime('now')),
  os_version    TEXT,
  uptime_sec    INTEGER,
  cpu_percent   REAL,
  mem_percent   REAL,
  disk_total_gb REAL,
  disk_free_gb  REAL,
  battery_percent INTEGER,      -- NULL on desktops
  battery_charging INTEGER,     -- 0/1/NULL
  updates_pending INTEGER NOT NULL DEFAULT 0,
  updates_security INTEGER NOT NULL DEFAULT 0,
  antivirus_ok  INTEGER,        -- 0/1/NULL
  raw_json      TEXT            -- full payload for anything not columned
);
CREATE INDEX IF NOT EXISTS idx_health_device_time
  ON health_reports(device_id, reported_at DESC);

-- Error/warning log entries the agent forwards (from the Windows Event Log).
CREATE TABLE IF NOT EXISTS log_events (
  id           INTEGER PRIMARY KEY AUTOINCREMENT,
  device_id    TEXT NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
  occurred_at  TEXT NOT NULL,
  level        TEXT NOT NULL DEFAULT 'error',  -- error | warning | info
  source       TEXT NOT NULL DEFAULT '',
  event_id     INTEGER,
  message      TEXT NOT NULL DEFAULT '',
  created_at   TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX IF NOT EXISTS idx_logs_device_time
  ON log_events(device_id, occurred_at DESC);

-- Commands queued for a device, picked up on next check-in.
CREATE TABLE IF NOT EXISTS commands (
  id           TEXT PRIMARY KEY,           -- uuid
  device_id    TEXT NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
  type         TEXT NOT NULL,              -- install_app | install_updates | restart | run_speedtest | scan_network
  payload_json TEXT NOT NULL DEFAULT '{}',
  status       TEXT NOT NULL DEFAULT 'pending', -- pending | sent | done | failed
  result_json  TEXT,
  created_by   INTEGER REFERENCES users(id) ON DELETE SET NULL,
  created_at   TEXT NOT NULL DEFAULT (datetime('now')),
  sent_at      TEXT,
  completed_at TEXT
);
CREATE INDEX IF NOT EXISTS idx_commands_device_status
  ON commands(device_id, status);

-- Help requests from the family help website.
CREATE TABLE IF NOT EXISTS tickets (
  id          INTEGER PRIMARY KEY AUTOINCREMENT,
  owner_id    INTEGER REFERENCES owners(id) ON DELETE SET NULL,
  from_name   TEXT NOT NULL DEFAULT '',
  topic       TEXT NOT NULL DEFAULT '',   -- printer | email | internet | other
  device_hint TEXT NOT NULL DEFAULT '',
  body        TEXT NOT NULL DEFAULT '',
  urgent      INTEGER NOT NULL DEFAULT 0,
  status      TEXT NOT NULL DEFAULT 'open', -- open | waiting | done
  created_at  TEXT NOT NULL DEFAULT (datetime('now')),
  updated_at  TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Replies on a ticket (from you or the family member).
CREATE TABLE IF NOT EXISTS ticket_messages (
  id          INTEGER PRIMARY KEY AUTOINCREMENT,
  ticket_id   INTEGER NOT NULL REFERENCES tickets(id) ON DELETE CASCADE,
  author      TEXT NOT NULL DEFAULT 'admin', -- admin | family
  body        TEXT NOT NULL DEFAULT '',
  created_at  TEXT NOT NULL DEFAULT (datetime('now'))
);
