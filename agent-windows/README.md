# Fleetwatch Windows agent

A small background **Windows Service** that runs on each computer you look
after. It checks in with the [server](../server) on a schedule to:

- report health (OS version, disk, memory, battery, antivirus status)
- report how many Windows Updates are pending (and how many are security ones)
- forward recent **error / warning** entries from the Windows Event Log
- run commands you queue from the dashboard: **install an app** (via winget),
  **install updates**, or **restart**

It's written in C# (.NET 8) and ships as a single self-contained `.exe`, so the
target PC does **not** need .NET installed.

## Getting the installer

You don't need to build this yourself. On every push, GitHub Actions compiles
the agent and the installer:

1. Go to the repo's **Actions** tab → latest **Build** run → download the
   **FleetwatchAgentSetup** artifact.
2. For a tagged release (`git tag v0.1.0 && git push --tags`), the installer is
   attached to the GitHub Release automatically.

## Installing on a computer

Run `FleetwatchAgentSetup.exe` as an administrator. The wizard asks for:

- **Server address** — e.g. `https://fleetwatch.example.com`
- **Enrollment code** — generate one in the dashboard / via
  `POST /api/enrollment-tokens` for that family member

You can also install silently (useful when you're setting up a relative's PC
over a remote session):

```
FleetwatchAgentSetup.exe /VERYSILENT /SERVERURL="https://fleetwatch.example.com" /ENROLLTOKEN="the-code"
```

The installer registers the **FleetwatchAgent** service (auto-start, auto-
restart on failure) and starts it. On first run the agent enrolls and saves its
device token to `C:\ProgramData\Fleetwatch\agent-state.json`.

## Building locally (optional)

Requires the .NET 8 SDK and (for the installer) Inno Setup 6.

```powershell
cd agent-windows
dotnet publish Fleetwatch.Agent.csproj -c Release -o publish
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" installer\fleetwatch-agent.iss
# installer appears in agent-windows\Output\FleetwatchAgentSetup.exe
```

To run it as a console app for testing (no service install):

```powershell
dotnet run --project Fleetwatch.Agent.csproj
```

Put your server URL + enrollment code in `appsettings.json` first.

## Configuration

`appsettings.json` (the installer writes this for you):

| Key | Meaning |
|---|---|
| `ServerUrl` | Base URL of your Fleetwatch server |
| `EnrollmentToken` | One-time code; only used until the device is enrolled |
| `CheckinIntervalSeconds` | How often to check in (default 300) |
| `DeviceName` | Optional friendly name; defaults to the computer name |

## Notes & limits

- **winget** must be present for app installs (it ships with Windows 10/11; on
  older builds it may need installing once).
- Installing Windows Updates and apps requires the service to run as
  LocalSystem, which the installer sets up.
- **Code signing:** the published exe and installer are unsigned, so Windows
  SmartScreen will warn on first run. For family use you can click through;
  for wider use you'd sign them with a code-signing certificate.
- Remote screen view/control is **not** in this agent yet — that's a later
  piece (planned on top of an open-source remote-desktop engine).
