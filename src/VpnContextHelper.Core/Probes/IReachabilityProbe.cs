namespace VpnContextHelper.Core.Probes;

/// <summary>
/// Abstraction over read-only reachability checks so the evaluator and tests do not touch the
/// network. The live implementation is <see cref="TcpDnsReachabilityProbe"/>.
/// </summary>
public interface IReachabilityProbe
{
    Task<ProbeResult> TcpAsync(string name, string host, int port, int timeoutMs, CancellationToken ct = default);
    Task<ProbeResult> DnsAsync(string name, string host, int timeoutMs, CancellationToken ct = default);
}
