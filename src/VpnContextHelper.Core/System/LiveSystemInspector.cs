using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using VpnContextHelper.Core.Net;

namespace VpnContextHelper.Core.SystemAccess;

/// <summary>
/// Read-only access to the live Windows networking state. Uses <c>System.Net.NetworkInformation</c>
/// for adapters and an <see cref="ICommandRunner"/> for the routing table. Performs NO changes.
/// </summary>
public sealed class LiveSystemInspector
{
    private readonly ICommandRunner _runner;

    public LiveSystemInspector(ICommandRunner? runner = null)
        => _runner = runner ?? new ProcessCommandRunner();

    public IReadOnlyList<AdapterInfo> GetAdapters()
    {
        var list = new List<AdapterInfo>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            IPAddress? ipv4 = null;
            try
            {
                ipv4 = nic.GetIPProperties().UnicastAddresses
                    .Select(u => u.Address)
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            }
            catch
            {
                // some pseudo-adapters throw on GetIPProperties — ignore
            }

            var mac = nic.GetPhysicalAddress()?.ToString();
            if (string.IsNullOrEmpty(mac)) mac = null;
            else mac = string.Join(":", Enumerable.Range(0, mac.Length / 2).Select(i => mac.Substring(i * 2, 2)));

            list.Add(new AdapterInfo(
                Name: nic.Name,
                Description: nic.Description,
                IPv4: ipv4,
                MacAddress: mac,
                MediaDisconnected: nic.OperationalStatus != OperationalStatus.Up));
        }
        return list;
    }

    public IReadOnlyList<IPAddress> GetFortinetAdapterIps()
        => GetAdapters().Where(a => a.IsFortinet && a.IPv4 is not null).Select(a => a.IPv4!).ToList();

    /// <summary>Connection-specific DNS suffixes of currently-up adapters (for trusted-network heuristics).</summary>
    public IReadOnlyList<string> GetConnectedDnsSuffixes()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            try
            {
                var suffix = nic.GetIPProperties().DnsSuffix;
                if (!string.IsNullOrWhiteSpace(suffix)) set.Add(suffix);
            }
            catch
            {
                // ignore adapters that do not expose IP properties
            }
        }
        return set.ToList();
    }

    public string CaptureRoutePrint() => _runner.Run("route.exe", "print -4").StdOut;

    public string CaptureIpConfig() => _runner.Run("ipconfig.exe", "/all").StdOut;

    public IReadOnlyList<RouteEntry> GetRoutes() => RouteParser.ParseRoutePrint(CaptureRoutePrint());

    /// <summary>Computes the current tunnel state from the live routing table and adapters.</summary>
    public VpnState GetVpnState()
    {
        var routes = GetRoutes();
        var vpnIps = GetFortinetAdapterIps();
        return VpnStateAnalyzer.Analyze(routes, vpnIps);
    }
}
