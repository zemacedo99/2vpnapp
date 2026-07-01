namespace VpnContextHelper.Core.Forti;

/// <summary>
/// Parses the output of <c>reg.exe query "&lt;key&gt;"</c> to extract the DIRECT child subkey leaf
/// names. Pure / no I/O. We only ever query the ".../Tunnels" container keys, whose direct children
/// are profile NAMES (not secret values), so nothing sensitive is read.
/// </summary>
public static class RegQueryParser
{
    /// <param name="regOutput">stdout of `reg query "&lt;parentKey&gt;"`.</param>
    /// <param name="parentKey">The queried key, e.g. HKEY_LOCAL_MACHINE\SOFTWARE\...\Sslvpn\Tunnels.</param>
    public static IReadOnlyList<string> ParseDirectSubkeyNames(string regOutput, string parentKey)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(regOutput)) return result;

        var prefix = parentKey.TrimEnd('\\') + "\\";
        foreach (var raw in regOutput.Split('\n'))
        {
            var line = raw.TrimEnd('\r').TrimEnd();
            if (!line.StartsWith("HKEY_", StringComparison.OrdinalIgnoreCase)) continue;
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

            var rest = line.Substring(prefix.Length);
            if (rest.Length == 0) continue;
            // Direct child only: no further backslash (subkey names may contain spaces, which is fine).
            if (rest.Contains('\\')) continue;
            result.Add(rest);
        }
        return result;
    }
}
