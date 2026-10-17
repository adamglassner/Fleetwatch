# Fleetwatch

Family computer management and assistance. Fleetwatch lets one person look
after the computers, phones and tablets used by their family — keeping them
updated, helping when something breaks, and installing software — without
needing to be in the room.

It is built for **families, not businesses**: plain language, gentle
defaults, and the device owner always in control.

> Status: early design stage. This repo currently holds UI mockups and
> planning notes. No application code yet.

## What it does

- **See every computer at a glance** — which are online, which have updates
  waiting, and which have had problems this week, grouped by person and home.
- **Keep computers updated remotely** — install OS and app updates in the
  background, with restarts deferred to the middle of the night so nobody
  loses their work.
- **Push software without anyone clicking** — pick apps and computers and
  install silently (where the platform allows it).
- **Read error logs remotely** — problems shown in plain English with the raw
  log underneath.
- **Remote help** — connect to a relative's computer to see their screen and
  help (view, or view + control depending on platform).
- **A help website for the family** — relatives create a ticket describing
  what's wrong; those land in an inbox on the admin side.
- **Network view** — internet health, every device on each home network, and
  saved links (router dashboard, ISP account, printer page).
- **Phones & tablets** — health info everywhere; remote **view** on
  iPhone/iPad, remote view + control on Android.

## Platforms

| Platform | Health info | Updates | Silent install | Remote view | Remote control |
|---|---|---|---|---|---|
| Windows | ✅ | ✅ | ✅ | ✅ | ✅ |
| Linux | ✅ | ✅ (system package manager) | ✅ | ✅ | ✅ |
| Android | ✅ | ✅ | ⚠️ needs a tap | ✅ | ✅ (accessibility opt-in) |
| GrapheneOS | ✅ | ✅ | ⚠️ needs a tap | ✅ | ✅ | help-only by default |
| iPhone / iPad | ✅ | ⚠️ prompts the user | ❌ (Apple) | ✅ | ❌ (Apple) — use FaceTime |

## Planned architecture

- **Agent** — a small background service on each computer (Windows service /
  systemd daemon) and a companion app on phones/tablets. It checks in with the
  server, applies updates, installs apps, reads logs, and handles remote
  sessions.
- **Server** — a self-hosted server the agents reach out to (outbound only, so
  no router setup at each home). Holds device status, tickets, and relays
  remote sessions. Likely Docker for easy self-hosting.
- **Admin dashboard** — the web app the admin uses (the mockups in this repo).
- **Family help website** — the simple ticket-creation site for relatives.
- **Remote engine** — built on an existing open-source remote-desktop stack
  (e.g. RustDesk / MeshCentral) rather than from scratch.

## Principles

- The device owner approves screen sharing every time, and can stop it any
  time. No hidden monitoring.
- Updates and restarts never interrupt what someone is doing.
- Free and self-hosted.

## Repo layout

```
server/          Linux server — Node + TypeScript + Fastify + SQLite  (see server/README.md)
agent-windows/   Windows agent — C# / .NET service + installer        (see agent-windows/README.md)
mockups/         UI mockups (Claude Design canvas source: *.dc.html + canvas.json)
.github/         CI: builds the server image and the agent installer
docker-compose.yml
```

The `.dc.html` files are the source for the design-canvas mockups and are
viewed inside the Claude artifact; they reference a canvas runtime and are not
standalone web pages.

## Building so far

The first two pieces are in place: the **server** and the **Windows agent**.

- **Server:** runs on Linux via Docker (`docker compose up -d --build`) or
  systemd. See [`server/README.md`](server/README.md).
- **Windows agent:** installed by `FleetwatchAgentSetup.exe`, which GitHub
  Actions builds on every push (Actions tab → latest run → artifacts). See
  [`agent-windows/README.md`](agent-windows/README.md).

What works today: device enrollment, scheduled check-ins with health + pending
Windows Updates + recent error logs, and remotely queued commands (install an
app via winget, install updates, restart). The admin dashboard UI, the family
help website, and remote screen view are still mockups.
