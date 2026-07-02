using VpnContextHelper.Core.Forti;
using VpnContextHelper.Core.Probes;

namespace VpnContextHelper.Core.Diagnostics;

/// <summary>
/// Orchestrates a read-only ZTNA resource reachability diagnosis.
/// Checks agent components, DNS, and TCP — in that order, short-circuiting where meaningful.
/// Never establishes connections beyond DNS lookup and a TCP handshake (no data sent).
/// </summary>
public sealed class ResourceDiagnosticRunner
{
    private readonly ZtnaAgentInspector _agentInspector;
    private readonly IReachabilityProbe _probe;
    private readonly int _timeoutMs;

    public ResourceDiagnosticRunner(IReachabilityProbe? probe = null, int timeoutMs = 3000)
    {
        _agentInspector = new ZtnaAgentInspector();
        _probe = probe ?? new TcpDnsReachabilityProbe();
        _timeoutMs = timeoutMs;
    }

    /// <param name="name">Display name for the resource.</param>
    /// <param name="host">Hostname to resolve and connect to.</param>
    /// <param name="port">TCP port to test (default 443).</param>
    /// <param name="category">Expected access category ("ztna"|"vpn"|"lan"|"internet"). Informational only.</param>
    public async Task<ResourceDiagnosis> DiagnoseAsync(
        string name, string host, int port = 443, string category = "ztna",
        CancellationToken ct = default)
    {
        var agentState = _agentInspector.Inspect();

        // DNS check first — TCP is only meaningful if DNS resolves.
        var dnsResult = await _probe.DnsAsync(name, host, _timeoutMs, ct).ConfigureAwait(false);

        // Attempt TCP only when DNS succeeded and the ZTNA proxy is running (otherwise it
        // confirms the proxy problem rather than a TCP-layer issue).
        ProbeResult? tcpResult = null;
        if (dnsResult.Ok)
            tcpResult = await _probe.TcpAsync(name, host, port, _timeoutMs, ct).ConfigureAwait(false);

        return new ResourceDiagnosis(name, host, port, category, agentState, dnsResult, tcpResult);
    }
}
