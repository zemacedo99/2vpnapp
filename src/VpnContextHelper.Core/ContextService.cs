using VpnContextHelper.Core.Config;
using VpnContextHelper.Core.Context;
using VpnContextHelper.Core.Diagnostics;
using VpnContextHelper.Core.Net;
using VpnContextHelper.Core.Probes;
using VpnContextHelper.Core.SystemAccess;

namespace VpnContextHelper.Core;

/// <summary>
/// Ties together the read-only inspector, reachability probes and the pure evaluator. Shared by
/// the CLI and the tray app so the logic lives in exactly one place. Performs no system changes.
/// </summary>
public sealed class ContextService
{
    private readonly AppConfig _config;
    private readonly LiveSystemInspector _inspector;
    private readonly IReachabilityProbe _probe;

    public ContextService(AppConfig config, LiveSystemInspector? inspector = null, IReachabilityProbe? probe = null)
    {
        _config = config;
        _inspector = inspector ?? new LiveSystemInspector();
        _probe = probe ?? new TcpDnsReachabilityProbe();
    }

    public string ToolVersion { get; init; } =
        typeof(ContextService).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    public async Task<EvaluationResult> EvaluateAsync(bool includeRawCaptures = true, CancellationToken ct = default)
    {
        var rawRoute = _inspector.CaptureRoutePrint();
        var routes = RouteParser.ParseRoutePrint(rawRoute);
        var adapters = _inspector.GetAdapters();
        var vpnIps = adapters.Where(a => a.IsFortinet && a.IPv4 is not null).Select(a => a.IPv4!).ToList();
        var vpn = VpnStateAnalyzer.Analyze(routes, vpnIps);

        var companyProbes = await ProbeGroupAsync(_config.CompanyAccess, ct).ConfigureAwait(false);
        var clientProbes = await ProbeGroupAsync(_config.ClientAccess, ct).ConfigureAwait(false);
        var trusted = await DetectTrustedAsync(ct).ConfigureAwait(false);

        var ctx = NetworkContextEvaluator.Build(_config, trusted, vpn, companyProbes, clientProbes);

        var data = new DiagnosticData
        {
            GeneratedAt = DateTimeOffset.Now,
            ToolVersion = ToolVersion,
            MachineName = EnvInfo.MachineName,
            OsCaption = EnvInfo.OsCaption,
            OsVersion = EnvInfo.OsVersion,
            FortiClientVersion = EnvInfo.FortiClientVersion(_config.FortiClientPath),
            Adapters = adapters,
            Routes = routes,
            RawRoutePrint = includeRawCaptures ? rawRoute : string.Empty,
            RawIpConfig = includeRawCaptures ? _inspector.CaptureIpConfig() : string.Empty,
        };

        return new EvaluationResult(ctx, data);
    }

    private async Task<IReadOnlyList<ProbeResult>> ProbeGroupAsync(AccessGroup group, CancellationToken ct)
    {
        var results = new List<ProbeResult>();
        foreach (var t in group.Targets)
        {
            if (string.IsNullOrWhiteSpace(t.Host)) continue;
            var r = await _probe.TcpAsync(t.Name, t.Host, t.Port, _config.ProbeTimeoutMs, ct).ConfigureAwait(false);
            results.Add(r with { ExpectVia = t.ExpectVia });
        }
        return results;
    }

    private async Task<bool> DetectTrustedAsync(CancellationToken ct)
    {
        var suffixes = _inspector.GetConnectedDnsSuffixes();
        if (_config.TrustedNetwork.DnsSuffixes.Any(want =>
                suffixes.Any(have => have.EndsWith(want, StringComparison.OrdinalIgnoreCase))))
            return true;

        foreach (var b in _config.TrustedNetwork.Beacons)
        {
            if (string.IsNullOrWhiteSpace(b.Host)) continue;
            var r = await _probe.TcpAsync(b.Name, b.Host, b.Port, _config.ProbeTimeoutMs, ct).ConfigureAwait(false);
            if (r.Ok) return true;
        }
        return false;
    }
}

public sealed record EvaluationResult(NetworkContext Context, DiagnosticData Data);
