using System.Net;
using System.Text.RegularExpressions;

namespace VpnContextHelper.Core.Net;

/// <summary>
/// Parses <c>ipconfig /all</c> output into <see cref="AdapterInfo"/> records. Pure / no I/O.
/// Used for the test fixtures and as a fallback; the live app prefers
/// <c>System.Net.NetworkInformation</c>.
/// </summary>
public static partial class IpConfigParser
{
    // "Ethernet adapter Ethernet 3:" / "Wireless LAN adapter Local Area Connection* 2:"
    [GeneratedRegex(@"^[\w ]+ adapter (?<name>.+):\s*$")]
    private static partial Regex AdapterHeader();

    public static IReadOnlyList<AdapterInfo> Parse(string text)
    {
        var result = new List<AdapterInfo>();
        if (string.IsNullOrEmpty(text)) return result;

        string? name = null, desc = null, mac = null;
        IPAddress? ipv4 = null;
        bool mediaDisconnected = false;
        bool open = false;

        void Flush()
        {
            if (open && name is not null)
                result.Add(new AdapterInfo(name, desc ?? string.Empty, ipv4, mac, mediaDisconnected));
            name = desc = mac = null;
            ipv4 = null;
            mediaDisconnected = false;
            open = false;
        }

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var header = AdapterHeader().Match(line);
            if (header.Success)
            {
                Flush();
                name = header.Groups["name"].Value.Trim();
                open = true;
                continue;
            }
            if (!open) continue;

            var t = line.Trim();
            if (t.StartsWith("Description", StringComparison.OrdinalIgnoreCase))
                desc = ValueAfterColon(t);
            else if (t.StartsWith("Physical Address", StringComparison.OrdinalIgnoreCase))
                mac = ValueAfterColon(t);
            else if (t.StartsWith("Media State", StringComparison.OrdinalIgnoreCase))
                mediaDisconnected = ValueAfterColon(t).Contains("disconnected", StringComparison.OrdinalIgnoreCase);
            else if (t.StartsWith("IPv4 Address", StringComparison.OrdinalIgnoreCase) && ipv4 is null)
            {
                var v = ValueAfterColon(t)
                    .Replace("(Preferred)", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("(Tentative)", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("(Duplicate)", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Trim();
                if (IPAddress.TryParse(v, out var ip)) ipv4 = ip;
            }
        }
        Flush();
        return result;
    }

    private static string ValueAfterColon(string line)
    {
        int i = line.IndexOf(':');
        return i >= 0 ? line[(i + 1)..].Trim() : string.Empty;
    }
}
