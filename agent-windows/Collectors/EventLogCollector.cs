using System.Diagnostics;
using System.Runtime.Versioning;

namespace Fleetwatch.Agent;

// Reads recent error/warning entries from the Windows Event Log (System +
// Application) so the admin can see "problems this week" without remoting in.
[SupportedOSPlatform("windows")]
public static class EventLogCollector
{
    public static List<LogEvent> RecentErrors(ILogger logger, DateTime since, int maxPerLog = 25)
    {
        var events = new List<LogEvent>();
        foreach (var logName in new[] { "System", "Application" })
        {
            try
            {
                using var log = new EventLog(logName);
                // Walk newest-first.
                var entries = log.Entries;
                int taken = 0;
                for (int i = entries.Count - 1; i >= 0 && taken < maxPerLog; i--)
                {
                    EventLogEntry e;
                    try { e = entries[i]; } catch { continue; }
                    if (e.TimeGenerated < since) break; // older than window; stop this log

                    if (e.EntryType != EventLogEntryType.Error &&
                        e.EntryType != EventLogEntryType.Warning)
                        continue;

                    events.Add(new LogEvent
                    {
                        OccurredAt = e.TimeGenerated.ToUniversalTime().ToString("o"),
                        Level = e.EntryType == EventLogEntryType.Error ? "error" : "warning",
                        Source = e.Source,
                        EventId = (int)(e.InstanceId & 0xFFFF),
                        Message = Truncate(e.Message, 1000),
                    });
                    taken++;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not read {Log} event log", logName);
            }
        }
        return events;
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max] + "…");
}
