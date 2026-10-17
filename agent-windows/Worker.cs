using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Extensions.Options;

namespace Fleetwatch.Agent;

// The long-running background service. Enrolls once, then checks in on a loop:
// report health + recent errors, run any queued commands, repeat.
[SupportedOSPlatform("windows")]
public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly AgentOptions _options;
    private readonly string _agentVersion;

    private ApiClient? _api;
    private AgentState? _state;
    private DateTime _lastLogScan = DateTime.UtcNow.AddDays(-1);

    public Worker(ILogger<Worker> logger, IOptions<AgentOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _agentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ServerUrl))
        {
            _logger.LogError("No ServerUrl configured. Edit appsettings.json.");
            return;
        }

        _api = new ApiClient(_options.ServerUrl);

        // Enroll if we haven't already.
        try
        {
            await EnsureEnrolledAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Enrollment failed; will keep retrying.");
        }

        var interval = TimeSpan.FromSeconds(Math.Max(30, _options.CheckinIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_state == null)
                    await EnsureEnrolledAsync(stoppingToken);

                if (_state != null)
                    await CheckinOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Check-in cycle failed; will retry next interval.");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task EnsureEnrolledAsync(CancellationToken ct)
    {
        _state = StateStore.Load();
        if (_state != null && !string.IsNullOrEmpty(_state.DeviceToken))
        {
            _api!.UseDeviceToken(_state.DeviceToken);
            _logger.LogInformation("Using existing device enrollment {DeviceId}", _state.DeviceId);
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.EnrollmentToken))
        {
            _logger.LogError("Not enrolled and no EnrollmentToken set. Edit appsettings.json.");
            return;
        }

        var req = new EnrollRequest
        {
            Token = _options.EnrollmentToken,
            Name = string.IsNullOrWhiteSpace(_options.DeviceName) ? Environment.MachineName : _options.DeviceName,
            Platform = "windows",
            OsVersion = HealthCollector.OsVersion(),
            AgentVersion = _agentVersion,
        };
        var resp = await _api!.EnrollAsync(req, ct);
        if (resp == null || string.IsNullOrEmpty(resp.DeviceToken))
            throw new InvalidOperationException("Enroll returned no token");

        _state = new AgentState { DeviceId = resp.DeviceId, DeviceToken = resp.DeviceToken };
        StateStore.Save(_state);
        _api.UseDeviceToken(_state.DeviceToken);
        _logger.LogInformation("Enrolled as device {DeviceId}", _state.DeviceId);
    }

    private async Task CheckinOnceAsync(CancellationToken ct)
    {
        var health = HealthCollector.Collect(_logger);
        var logs = EventLogCollector.RecentErrors(_logger, _lastLogScan);
        _lastLogScan = DateTime.UtcNow;

        var req = new CheckinRequest
        {
            OsVersion = HealthCollector.OsVersion(),
            AgentVersion = _agentVersion,
            Health = health,
            Logs = logs,
        };

        var resp = await _api!.CheckinAsync(req, ct);
        if (resp == null) return;

        _logger.LogInformation(
            "Checked in. {Updates} updates pending, {Logs} new log events, {Cmds} commands.",
            health.UpdatesPending, logs.Count, resp.Commands.Count);

        if (resp.Commands.Count > 0)
        {
            var exec = new CommandExecutor(_logger);
            foreach (var cmd in resp.Commands)
            {
                if (ct.IsCancellationRequested) break;
                _logger.LogInformation("Running command {Type} ({Id})", cmd.Type, cmd.Id);
                var result = await exec.RunAsync(cmd, ct);
                try { await _api.ReportResultAsync(cmd.Id, result, ct); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not report result for {Id}", cmd.Id); }
            }
        }
    }
}
