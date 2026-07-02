using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace VpnContextHelper.Core.Forti;

/// <summary>
/// Read-only snapshot of the ZTNA / Zero Trust agent component state.
/// Distinguishes the EMS management-plane connection (FortiESNAC) from the
/// ZTNA data-plane proxy (FortiProxy + FortiTcs), which can diverge.
/// </summary>
public sealed record ZtnaAgentState(
    bool ProxyRunning,          // FortiProxy — the ZTNA access-proxy process
    bool TelemetryRunning,      // FortiTcs   — Zero Trust compliance/telemetry
    bool EmsRunning,            // FortiESNAC — EMS management-plane (what UI "Connected" reflects)
    bool SslVpnDaemonRunning,   // FortiSSLVPNdaemon
    bool VpnRunning,            // FortiVPN
    bool TrayRunning,           // FortiTray
    IReadOnlyList<int> ActiveListenerPorts,
    IReadOnlyList<(string InterfaceAlias, IReadOnlyList<string> DnsServers)> DnsServersByInterface)
{
    public static readonly int[] ExpectedZtnaListenerPorts = [999, 17713, 17714, 17715];

    /// <summary>True only when the ZTNA access-proxy is up with at least one listener port.</summary>
    public bool ZtnaDataPlaneActive => ProxyRunning && ActiveListenerPorts.Count > 0;

    public string DiagnosisSummary()
    {
        if (!ProxyRunning && !TelemetryRunning)
            return "FortiProxy (ZTNA proxy) and FortiTcs (Zero Trust telemetry) are both NOT running. " +
                   "The ZTNA data plane is fully inactive even though the FortiClient UI may show " +
                   "'Zero Trust Telemetry: Connected' (that reflects FortiESNAC / EMS registration, not the ZTNA proxy).";

        if (!ProxyRunning)
            return "FortiProxy (ZTNA proxy) is NOT running. FortiTcs is running but the access proxy " +
                   "is down — resources served via ZTNA are unreachable.";

        if (ActiveListenerPorts.Count == 0)
            return "FortiProxy is running but has no ZTNA listener ports open " +
                   "(expected: 999, 17713–17715). ZTNA interception is not active.";

        return "FortiProxy is running and ZTNA listener ports are present. ZTNA access proxy appears operational.";
    }

    /// <summary>
    /// Returns true if only client-VPN DNS servers are visible (all interfaces share the same
    /// server set and none look like a company/office resolver, i.e. no RFC-1918 /16-/24 with an
    /// alias that suggests a company adapter).
    /// This is a heuristic observation, not a definitive test.
    /// </summary>
    public bool ClientVpnDnsOverridesAllInterfaces()
    {
        if (DnsServersByInterface.Count == 0) return false;
        // If every interface reports the same small set of servers, the client VPN likely pushed its
        // DNS globally. We detect this by checking whether all server sets are equal.
        var first = DnsServersByInterface[0].DnsServers;
        return DnsServersByInterface.All(p =>
            p.DnsServers.Count == first.Count &&
            p.DnsServers.OrderBy(x => x).SequenceEqual(first.OrderBy(x => x)));
    }

    /// <summary>Returns the union set of all DNS servers across all interfaces (for reporting).</summary>
    public IReadOnlyList<string> AllDnsServers()
        => DnsServersByInterface.SelectMany(p => p.DnsServers).Distinct().ToList();
}
