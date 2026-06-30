namespace VpnContextHelper.Core.Config;

/// <summary>Finds and loads <see cref="AppConfig"/> from the conventional locations.</summary>
public static class ConfigLocator
{
    public static IReadOnlyList<string> Candidates(string? explicitPath)
    {
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(explicitPath)) list.Add(explicitPath!);
        list.Add(Path.Combine(Environment.CurrentDirectory, "config", "config.json"));
        list.Add(Path.Combine(Environment.CurrentDirectory, "config.json"));
        list.Add(Path.Combine(AppContext.BaseDirectory, "config", "config.json"));
        list.Add(Path.Combine(AppContext.BaseDirectory, "config.json"));
        return list;
    }

    public static (AppConfig Config, string Source) Find(string? explicitPath = null, Action<string>? onError = null)
    {
        foreach (var c in Candidates(explicitPath))
        {
            if (!File.Exists(c)) continue;
            try { return (AppConfig.Load(c), c); }
            catch (Exception ex) { onError?.Invoke($"Failed to read config '{c}': {ex.Message}"); }
        }
        return (new AppConfig(), "(built-in defaults — copy config/config.sample.json to config/config.json)");
    }
}
