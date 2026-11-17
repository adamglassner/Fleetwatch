# Fleetwatch client (Windows)

The desktop app a person installs on their own PC — the **client/shell**. This
is what the Bytehawk launcher installs: a normal per-user WPF app (no admin, no
service), so it drops in and launches cleanly like the other Bytehawk apps.

It's an early shell right now: it opens, shows the Fleetwatch branding and the
sections that are coming, and nothing more. Features land here over time.

> Not to be confused with [`../agent-windows`](../agent-windows), the background
> **service** that runs on each managed computer, or [`../server`](../server),
> the Linux server. Those are installed differently (the agent via its own
> admin installer); the launcher installs *this* client.

## Build locally

Requires the .NET 8 SDK.

```powershell
cd client-windows
dotnet run                      # run the shell
# or produce the shippable build:
dotnet publish -c Release -o publish
```

CI publishes this self-contained (win-x64) and zips it as
`Fleetwatch-<version>-win-x64.zip`, attached to each release — that's the asset
the launcher downloads.
