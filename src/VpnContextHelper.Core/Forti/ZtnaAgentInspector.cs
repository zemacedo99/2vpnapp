using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace VpnContextHelper.Core.Forti;

/// <summary>
/// Read-only inspector for the FortiClient ZTNA agent components.
/// Uses only standard .NET APIs (Process, IPGlobalProperties, NetworkInterface) — no elevation.
/// Never reads secret values, credentials, VPN config, or certificate stores.
/// </summary>
public sealed class ZtnaAgentInspector
{
    private static bool IsRunning(string processName)
    {
        var procs = Process.GetProcessesByName(processName);
        var found = procs.Length > 0;
        foreach (var p in procs) p.Dispose();
        return found;
    }

    private static IReadOnlyList<int> ActiveZtnaListeners()
    {
        var found = new List<int>();
        try
        {
            var props = IPGlobalProperties.GetIPGlobalProperties();
            var listeners = props.GetActiveTcpListeners();
            foreach (var ep in listeners)
            {
                if (ZtnaAgentState.ExpectedZtnaListenerPorts.Contains(ep.Port))
                    found.Add(ep.Port);
            }
        }
        catch
        {
            // GetActiveTcpListeners can fail on some privilege configurations — treat as empty
        }
        return found;
    }

    private static IReadOnlyList<(string InterfaceAlias, IReadOnlyList<string> DnsServers)> DnsServersByInterface()
    {
        var result = new List<(string, IReadOnlyList<string>)>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                try
                {
                    var dns = nic.GetIPProperties().DnsAddresses
                        .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                        .Select(a => a.ToString())
                        .ToList();
                    if (dns.Count > 0)
                        result.Add((nic.Name, (IReadOnlyList<string>)dns));
                }
                catch { /* skip interfaces that throw */ }
            }
        }
        catch { /* ignore if NetworkInterface.GetAllNetworkInterfaces() itself fails */ }
        return result;
    }

    public ZtnaAgentState Inspect() => new(
        ProxyRunning: IsRunning("FortiProxy"),
        TelemetryRunning: IsRunning("FortiTcs"),
        EmsRunning: IsRunning("FortiESNAC"),
        SslVpnDaemonRunning: IsRunning("FortiSSLVPNdaemon"),
        VpnRunning: IsRunning("FortiVPN"),
        TrayRunning: IsRunning("FortiTray"),
        ActiveListenerPorts: ActiveZtnaListeners(),
        DnsServersByInterface: DnsServersByInterface());
}
