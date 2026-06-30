using System.Net;
using System.Text.RegularExpressions;

namespace VpnContextHelper.Core.Net;

/// <summary>
/// Parses the IPv4 "Active Routes" section of <c>route print -4</c> output into
/// <see cref="RouteEntry"/> records. Pure / no I/O so it can be unit-tested against
/// captured fixtures.
/// </summary>
public static partial class RouteParser
{
    // e.g. "          0.0.0.0          0.0.0.0      192.168.1.1      192.168.1.10     35"
    //      "      172.16.0.0      255.240.0.0        10.20.0.1        10.20.0.2      1"
    [GeneratedRegex(
        @"^\s*(?<dest>\d{1,3}(?:\.\d{1,3}){3})\s+(?<mask>\d{1,3}(?:\.\d{1,3}){3})\s+(?<gw>On-link|\d{1,3}(?:\.\d{1,3}){3})\s+(?<iface>\d{1,3}(?:\.\d{1,3}){3})\s+(?<metric>\d+)\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex RouteLine();

    public static IReadOnlyList<RouteEntry> ParseRoutePrint(string text)
    {
        var entries = new List<RouteEntry>();
        if (string.IsNullOrEmpty(text)) return entries;

        bool inActive = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var trimmed = line.Trim();

            if (trimmed.StartsWith("Active Routes:", StringComparison.OrdinalIgnoreCase))
            {
                inActive = true;
                continue;
            }
            if (trimmed.StartsWith("Persistent Routes:", StringComparison.OrdinalIgnoreCase))
            {
                inActive = false;
                continue;
            }
            if (!inActive) continue;
            if (trimmed.StartsWith("Network Destination", StringComparison.OrdinalIgnoreCase)) continue;
            if (trimmed.StartsWith("====="))
            {
                inActive = false;
                continue;
            }

            var m = RouteLine().Match(line);
            if (!m.Success) continue;
            if (!IPAddress.TryParse(m.Groups["dest"].Value, out var dest)) continue;
            if (!IPAddress.TryParse(m.Groups["mask"].Value, out var mask)) continue;
            if (!IPAddress.TryParse(m.Groups["iface"].Value, out var iface)) continue;
            int.TryParse(m.Groups["metric"].Value, out var metric);
            entries.Add(new RouteEntry(dest, mask, m.Groups["gw"].Value, iface, metric));
        }
        return entries;
    }
}
