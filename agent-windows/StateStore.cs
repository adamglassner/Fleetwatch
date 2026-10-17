using System.Text.Json;

namespace Fleetwatch.Agent;

// Persists the device id + token under ProgramData so enrollment survives
// restarts. Only the local SYSTEM/admin account can read it by default.
public static class StateStore
{
    private static string Dir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Fleetwatch");

    private static string FilePath => Path.Combine(Dir, "agent-state.json");

    public static AgentState? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AgentState>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void Save(AgentState state)
    {
        Directory.CreateDirectory(Dir);
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }
}
