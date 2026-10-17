using System.Text.Json.Serialization;

namespace Fleetwatch.Agent;

// Settings bound from appsettings.json ("Fleetwatch" section) and, after
// enrollment, from the saved state file in ProgramData.
public sealed class AgentOptions
{
    public string ServerUrl { get; set; } = "";
    public string EnrollmentToken { get; set; } = "";
    public int CheckinIntervalSeconds { get; set; } = 300;
    public string DeviceName { get; set; } = "";
}

// Persisted after a successful enroll so we never re-enroll.
public sealed class AgentState
{
    public string DeviceId { get; set; } = "";
    public string DeviceToken { get; set; } = "";
}

// ---- API payloads ----

public sealed class EnrollRequest
{
    [JsonPropertyName("token")] public string Token { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("platform")] public string Platform { get; set; } = "windows";
    [JsonPropertyName("os_version")] public string OsVersion { get; set; } = "";
    [JsonPropertyName("agent_version")] public string AgentVersion { get; set; } = "";
}

public sealed class EnrollResponse
{
    [JsonPropertyName("device_id")] public string DeviceId { get; set; } = "";
    [JsonPropertyName("device_token")] public string DeviceToken { get; set; } = "";
}

public sealed class HealthSnapshot
{
    [JsonPropertyName("uptime_sec")] public long UptimeSec { get; set; }
    [JsonPropertyName("cpu_percent")] public double? CpuPercent { get; set; }
    [JsonPropertyName("mem_percent")] public double? MemPercent { get; set; }
    [JsonPropertyName("disk_total_gb")] public double? DiskTotalGb { get; set; }
    [JsonPropertyName("disk_free_gb")] public double? DiskFreeGb { get; set; }
    [JsonPropertyName("battery_percent")] public int? BatteryPercent { get; set; }
    [JsonPropertyName("battery_charging")] public bool? BatteryCharging { get; set; }
    [JsonPropertyName("updates_pending")] public int UpdatesPending { get; set; }
    [JsonPropertyName("updates_security")] public int UpdatesSecurity { get; set; }
    [JsonPropertyName("antivirus_ok")] public bool? AntivirusOk { get; set; }
}

public sealed class LogEvent
{
    [JsonPropertyName("occurred_at")] public string OccurredAt { get; set; } = "";
    [JsonPropertyName("level")] public string Level { get; set; } = "error";
    [JsonPropertyName("source")] public string Source { get; set; } = "";
    [JsonPropertyName("event_id")] public int? EventId { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = "";
}

public sealed class CheckinRequest
{
    [JsonPropertyName("os_version")] public string OsVersion { get; set; } = "";
    [JsonPropertyName("agent_version")] public string AgentVersion { get; set; } = "";
    [JsonPropertyName("health")] public HealthSnapshot Health { get; set; } = new();
    [JsonPropertyName("logs")] public List<LogEvent> Logs { get; set; } = new();
}

public sealed class AgentCommand
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("payload")] public System.Text.Json.JsonElement Payload { get; set; }
}

public sealed class CheckinResponse
{
    [JsonPropertyName("commands")] public List<AgentCommand> Commands { get; set; } = new();
    [JsonPropertyName("checkin_interval_sec")] public int? CheckinIntervalSec { get; set; }
}

public sealed class CommandResult
{
    [JsonPropertyName("status")] public string Status { get; set; } = "done";
    [JsonPropertyName("result")] public object? Result { get; set; }
}
