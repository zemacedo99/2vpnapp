using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace VpnContextHelper.Core.Probes;

/// <summary>
/// Live reachability probe. Performs a TCP connect (no data sent) or a DNS lookup with a timeout.
/// It opens and immediately closes the socket — it never sends or reads application data, so no
/// secrets, cookies, or tokens are ever observed.
/// </summary>
public sealed class TcpDnsReachabilityProbe : IReachabilityProbe
{
    public async Task<ProbeResult> TcpAsync(string name, string host, int port, int timeoutMs, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var target = $"{host}:{port}";
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            try
            {
                await client.ConnectAsync(host, port, cts.Token).AsTask();
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return new ProbeResult(name, target, ProbeKind.Tcp, false, sw.ElapsedMilliseconds, "timeout");
            }
            return new ProbeResult(name, target, ProbeKind.Tcp, client.Connected, sw.ElapsedMilliseconds,
                client.Connected ? "connected" : "not connected");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // genuine caller cancellation must propagate, not become a failed result
        }
        catch (Exception ex)
        {
            return new ProbeResult(name, target, ProbeKind.Tcp, false, sw.ElapsedMilliseconds, Describe(ex));
        }
    }

    public async Task<ProbeResult> DnsAsync(string name, string host, int timeoutMs, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            IPAddress[] addrs;
            try
            {
                addrs = await Dns.GetHostAddressesAsync(host, cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return new ProbeResult(name, host, ProbeKind.Dns, false, sw.ElapsedMilliseconds, "timeout");
            }
            return new ProbeResult(name, host, ProbeKind.Dns, addrs.Length > 0, sw.ElapsedMilliseconds,
                addrs.Length > 0 ? $"{addrs.Length} address(es)" : "no records");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // genuine caller cancellation must propagate
        }
        catch (Exception ex)
        {
            return new ProbeResult(name, host, ProbeKind.Dns, false, sw.ElapsedMilliseconds, Describe(ex));
        }
    }

    private static string Describe(Exception ex) => ex switch
    {
        SocketException se => se.SocketErrorCode.ToString(),
        _ => ex.GetType().Name,
    };
}
