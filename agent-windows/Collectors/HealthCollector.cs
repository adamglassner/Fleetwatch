using System.Diagnostics;
using System.Runtime.Versioning;
using System.Management;

namespace Fleetwatch.Agent;

// Gathers a health snapshot of this Windows PC.
[SupportedOSPlatform("windows")]
public static class HealthCollector
{
    public static HealthSnapshot Collect(ILogger logger)
    {
        var h = new HealthSnapshot();

        try
        {
            h.UptimeSec = (long)(TimeSpan.FromMilliseconds(Environment.TickCount64)).TotalSeconds;
        }
        catch (Exception ex) { logger.LogWarning(ex, "uptime"); }

        // Disk (system drive)
        try
        {
            var sysRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(sysRoot);
            if (drive.IsReady)
            {
                h.DiskTotalGb = Math.Round(drive.TotalSize / 1024d / 1024 / 1024, 1);
                h.DiskFreeGb = Math.Round(drive.AvailableFreeSpace / 1024d / 1024 / 1024, 1);
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "disk"); }

        // Memory + battery via WMI
        try
        {
            using var os = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject mo in os.Get())
            {
                var total = Convert.ToDouble(mo["TotalVisibleMemorySize"]); // KB
                var free = Convert.ToDouble(mo["FreePhysicalMemory"]);      // KB
                if (total > 0) h.MemPercent = Math.Round((total - free) / total * 100, 1);
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "memory"); }

        try
        {
            using var batt = new ManagementObjectSearcher(
                "SELECT EstimatedChargeRemaining, BatteryStatus FROM Win32_Battery");
            foreach (ManagementObject mo in batt.Get())
            {
                if (mo["EstimatedChargeRemaining"] != null)
                    h.BatteryPercent = Convert.ToInt32(mo["EstimatedChargeRemaining"]);
                if (mo["BatteryStatus"] != null)
                {
                    // 2 = on AC power
                    var status = Convert.ToInt32(mo["BatteryStatus"]);
                    h.BatteryCharging = status == 2;
                }
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "battery (likely a desktop)"); }

        // Antivirus (Windows Security Center)
        try
        {
            using var av = new ManagementObjectSearcher(
                @"root\SecurityCenter2", "SELECT productState FROM AntiVirusProduct");
            foreach (ManagementObject mo in av.Get())
            {
                // Bit 0x1000 of the middle byte means "enabled".
                var state = Convert.ToInt32(mo["productState"]);
                var enabled = (state & 0x1000) != 0;
                h.AntivirusOk = enabled;
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "antivirus"); }

        // Windows updates
        try
        {
            var (pending, security) = UpdateCollector.CountPending(logger);
            h.UpdatesPending = pending;
            h.UpdatesSecurity = security;
        }
        catch (Exception ex) { logger.LogWarning(ex, "updates"); }

        return h;
    }

    public static string OsVersion()
    {
        try
        {
            using var os = new ManagementObjectSearcher("SELECT Caption, Version FROM Win32_OperatingSystem");
            foreach (ManagementObject mo in os.Get())
                return $"{mo["Caption"]} ({mo["Version"]})".Trim();
        }
        catch { /* fall through */ }
        return Environment.OSVersion.VersionString;
    }
}
