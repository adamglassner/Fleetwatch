using System.Runtime.Versioning;

namespace Fleetwatch.Agent;

// Counts pending Windows Updates using the COM Windows Update Agent (WUA) API.
// Reflection/COM is used so we don't need a typed interop assembly.
[SupportedOSPlatform("windows")]
public static class UpdateCollector
{
    // Returns (totalPending, securityPending).
    public static (int total, int security) CountPending(ILogger logger)
    {
        try
        {
            var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
            if (sessionType == null) return (0, 0);

            dynamic session = Activator.CreateInstance(sessionType)!;
            dynamic searcher = session.CreateUpdateSearcher();
            // Only search the local cache so this is quick and offline-safe.
            searcher.Online = false;
            dynamic result = searcher.Search("IsInstalled=0 And IsHidden=0");
            dynamic updates = result.Updates;

            int total = updates.Count;
            int security = 0;
            for (int i = 0; i < total; i++)
            {
                dynamic u = updates.Item(i);
                dynamic categories = u.Categories;
                for (int c = 0; c < categories.Count; c++)
                {
                    string name = categories.Item(c).Name?.ToString() ?? "";
                    if (name.Contains("Security", StringComparison.OrdinalIgnoreCase))
                    {
                        security++;
                        break;
                    }
                }
            }
            return (total, security);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not query Windows Update");
            return (0, 0);
        }
    }

    // Triggers download + install of all pending updates. Returns a short summary.
    public static string InstallAll(ILogger logger)
    {
        var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session")
            ?? throw new InvalidOperationException("Windows Update is not available");
        dynamic session = Activator.CreateInstance(sessionType)!;
        dynamic searcher = session.CreateUpdateSearcher();
        dynamic result = searcher.Search("IsInstalled=0 And IsHidden=0");
        dynamic updates = result.Updates;
        if (updates.Count == 0) return "No updates pending.";

        var updateCollType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl")!;
        dynamic toInstall = Activator.CreateInstance(updateCollType)!;
        for (int i = 0; i < updates.Count; i++)
        {
            dynamic u = updates.Item(i);
            if (!u.EulaAccepted) { try { u.AcceptEula(); } catch { } }
            toInstall.Add(u);
        }

        dynamic downloader = session.CreateUpdateDownloader();
        downloader.Updates = toInstall;
        downloader.Download();

        dynamic installer = session.CreateUpdateInstaller();
        installer.Updates = toInstall;
        dynamic installResult = installer.Install();

        int code = installResult.ResultCode; // 2 = succeeded
        bool reboot = installResult.RebootRequired;
        return $"Installed {toInstall.Count} update(s), result code {code}, reboot required: {reboot}";
    }
}
