using System.Net;

namespace VpnContextHelper.Core.Net;

/// <summary>
/// The derived state of the (single) active FortiClient tunnel, computed from the routing
/// table plus the set of known Fortinet/VPN virtual-adapter IPv4 addresses.
/// </summary>
public sealed record VpnState(
    bool Connected,
    IPAddress? TunnelIp,
    bool IsFullTunnel,
    IReadOnlyList<Cidr> TunneledCidrs)
{
    public bool IsSplitTunnel => Connected && !IsFullTunnel && TunneledCidrs.Count > 0;

    public static VpnState Disconnected { get; } =
        new(false, null, false, Array.Empty<Cidr>());
}

/// <summary>Derives <see cref="VpnState"/> from parsed routes. Pure function.</summary>
public static class VpnStateAnalyzer
{
    private static readonly Cidr Multicast = Cidr.Parse("224.0.0.0/4");
    private static readonly IPAddress Broadcast = IPAddress.Parse("255.255.255.255");

    /// <param name="routes">Parsed active routes.</param>
    /// <param name="vpnInterfaceIps">
    /// IPv4 addresses assigned to Fortinet/VPN virtual adapters (e.g. 10.20.0.2). Routes whose
    /// Interface column matches one of these are considered to be carried by the tunnel.
    /// </param>
    public static VpnState Analyze(IEnumerable<RouteEntry> routes, IEnumerable<IPAddress> vpnInterfaceIps)
    {
        var vpnIps = new HashSet<string>(vpnInterfaceIps.Select(ip => ip.ToString()));
        if (vpnIps.Count == 0) return VpnState.Disconnected;

        var onTunnel = routes.Where(r => vpnIps.Contains(r.InterfaceIp.ToString())).ToList();
        if (onTunnel.Count == 0) return VpnState.Disconnected;

        IPAddress? tunnelIp = onTunnel
            .Select(r => r.InterfaceIp)
            .FirstOrDefault(ip => vpnIps.Contains(ip.ToString()));

        bool full = onTunnel.Any(r => r.IsDefaultRoute);

        var tunneled = new List<Cidr>();
        foreach (var r in onTunnel)
        {
            if (r.IsDefaultRoute)
            {
                tunneled.Add(r.ToCidr());
                continue;
            }
            if (Multicast.Contains(r.Destination)) continue;                 // 224.0.0.0/4
            if (r.Destination.Equals(Broadcast)) continue;                   // 255.255.255.255
            if (r.PrefixLength == 32 && tunnelIp != null && r.Destination.Equals(tunnelIp))
                continue;                                                    // adapter's own /32
            tunneled.Add(r.ToCidr());
        }

        return new VpnState(true, tunnelIp, full, tunneled.Distinct().ToList());
    }
}
