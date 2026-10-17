using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

namespace Fleetwatch.Agent;

// Runs the commands the server queues: install an app, install updates, restart.
[SupportedOSPlatform("windows")]
public sealed class CommandExecutor
{
    private readonly ILogger _logger;

    public CommandExecutor(ILogger logger) => _logger = logger;

    public async Task<CommandResult> RunAsync(AgentCommand cmd, CancellationToken ct)
    {
        try
        {
            switch (cmd.Type)
            {
                case "install_app":
                    return InstallApp(cmd.Payload);
                case "install_updates":
                    return new CommandResult { Status = "done", Result = UpdateCollector.InstallAll(_logger) };
                case "restart":
                    return Restart(cmd.Payload);
                default:
                    return new CommandResult { Status = "failed", Result = $"Unknown command type '{cmd.Type}'" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command {Type} failed", cmd.Type);
            return new CommandResult { Status = "failed", Result = ex.Message };
        }
        finally
        {
            await Task.CompletedTask;
        }
    }

    // Install an app silently via winget. Payload: { "id": "Mozilla.Firefox" }.
    private CommandResult InstallApp(JsonElement payload)
    {
        if (!payload.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.String)
            return new CommandResult { Status = "failed", Result = "Missing app id" };

        var id = idEl.GetString()!;
        // Basic guard: winget package ids are alphanumerics, dots, dashes, pluses.
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, @"^[A-Za-z0-9.\-+_]{1,100}$"))
            return new CommandResult { Status = "failed", Result = "Invalid app id" };

        var args = $"install --id {id} --exact --silent " +
                   "--accept-package-agreements --accept-source-agreements " +
                   "--disable-interactivity";
        var (code, output) = RunProcess("winget", args, TimeSpan.FromMinutes(20));
        return new CommandResult
        {
            Status = code == 0 ? "done" : "failed",
            Result = new { exitCode = code, output = Tail(output, 2000) },
        };
    }

    // Restart. Payload: { "delay_sec": 60 } (optional).
    private CommandResult Restart(JsonElement payload)
    {
        int delay = 60;
        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("delay_sec", out var d) && d.TryGetInt32(out var ds))
            delay = Math.Clamp(ds, 0, 3600);

        var (code, output) = RunProcess("shutdown", $"/r /t {delay} /c \"Fleetwatch: restarting to finish updates\"", TimeSpan.FromSeconds(30));
        return new CommandResult
        {
            Status = code == 0 ? "done" : "failed",
            Result = new { scheduledInSec = delay, exitCode = code, output = Tail(output, 500) },
        };
    }

    private (int code, string output) RunProcess(string file, string args, TimeSpan timeout)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = new Process { StartInfo = psi };
        var sb = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
        _logger.LogInformation("Running: {File} {Args}", file, args);
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        if (!p.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            return (-1, sb.ToString() + "\n[timed out]");
        }
        return (p.ExitCode, sb.ToString());
    }

    private static string Tail(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : "…" + s[^max..]);
}
