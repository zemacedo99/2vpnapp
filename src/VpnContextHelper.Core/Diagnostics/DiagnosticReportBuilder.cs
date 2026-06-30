using System.Text;
using VpnContextHelper.Core.Config;
using VpnContextHelper.Core.Context;
using VpnContextHelper.Core.Net;

namespace VpnContextHelper.Core.Diagnostics;

/// <summary>
/// Renders a Markdown diagnostic report that the user can hand to IT. Everything is passed
/// through the supplied <see cref="Redactor"/> so no secrets leave the machine.
/// </summary>
public static class DiagnosticReportBuilder
{
    public static string Build(AppConfig config, DiagnosticData data, NetworkContext ctx, Redactor redactor)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# VPN Context Helper — Diagnostic Report");
        sb.AppendLine();
        sb.AppendLine($"- Generated: {data.GeneratedAt:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"- Tool version: {data.ToolVersion}");
        sb.AppendLine($"- Machine: {data.MachineName}");
        sb.AppendLine($"- OS: {data.OsCaption} ({data.OsVersion})");
        sb.AppendLine($"- FortiClient: {data.FortiClientVersion}");
        sb.AppendLine();
        sb.AppendLine("> This report is read-only evidence. It contains no credentials, tokens, cookies, ");
        sb.AppendLine("> SAML assertions or private keys — these are removed by the built-in redactor.");
        sb.AppendLine();

        sb.AppendLine("## Summary");
        sb.AppendLine();
        foreach (var m in ctx.Messages) sb.AppendLine($"- {m}");
        sb.AppendLine();

        sb.AppendLine("## VPN tunnel state");
        sb.AppendLine();
        if (ctx.Vpn.Connected)
        {
            sb.AppendLine($"- Connected: yes (tunnel IP `{ctx.Vpn.TunnelIp}`)");
            sb.AppendLine($"- Mode: {(ctx.Vpn.IsFullTunnel ? "full tunnel (default route on tunnel)" : "split tunnel")}");
            sb.AppendLine("- Routed ranges (carried by the tunnel):");
            foreach (var c in ctx.Vpn.TunneledCidrs) sb.AppendLine($"  - `{c}`");
        }
        else
        {
            sb.AppendLine("- Connected: no active FortiClient tunnel detected");
        }
        sb.AppendLine();

        AppendGroup(sb, ctx.Company);
        AppendGroup(sb, ctx.Client);

        if (ctx.RouteOverlaps.Count > 0)
        {
            sb.AppendLine("## Route / addressing overlaps");
            sb.AppendLine();
            sb.AppendLine("These company ranges fall inside the currently-tunneled ranges, so they are captured");
            sb.AppendLine("by the client tunnel while it is connected:");
            sb.AppendLine();
            foreach (var (t, c) in ctx.RouteOverlaps)
                sb.AppendLine($"- company `{c}` is inside tunnel `{t}`");
            sb.AppendLine();
        }

        sb.AppendLine("## Network adapters (with IPv4 or VPN/virtual)");
        sb.AppendLine();
        sb.AppendLine("| Adapter | Description | IPv4 | State |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var a in data.Adapters.Where(a => a.IsInteresting))
        {
            var state = a.MediaDisconnected ? "disconnected" : "up";
            var flag = a.IsFortinet ? " **(Fortinet)**" : a.IsVpnVirtual ? " (vpn/virtual)" : string.Empty;
            var ip = a.HasRoutableIPv4 ? a.IPv4!.ToString() : "-";
            sb.AppendLine($"| {a.Name} | {a.Description}{flag} | {ip} | {state} |");
        }
        sb.AppendLine();

        sb.AppendLine("## Active IPv4 routes (parsed)");
        sb.AppendLine();
        sb.AppendLine("```");
        foreach (var r in data.Routes) sb.AppendLine(r.ToString());
        sb.AppendLine("```");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(data.RawRoutePrint))
        {
            sb.AppendLine("<details><summary>Appendix A — raw <code>route print -4</code></summary>");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(data.RawRoutePrint.Trim());
            sb.AppendLine("```");
            sb.AppendLine("</details>");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(data.RawIpConfig))
        {
            sb.AppendLine("<details><summary>Appendix B — raw <code>ipconfig /all</code></summary>");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(data.RawIpConfig.Trim());
            sb.AppendLine("```");
            sb.AppendLine("</details>");
            sb.AppendLine();
        }

        return redactor.Redact(sb.ToString());
    }

    private static void AppendGroup(StringBuilder sb, GroupStatus g)
    {
        sb.AppendLine($"## {g.Label} reachability — {g.Verdict}");
        sb.AppendLine();
        if (g.Probes.Count == 0)
        {
            sb.AppendLine("- (no targets configured)");
            sb.AppendLine();
            return;
        }
        sb.AppendLine("| Result | Check | Target | Expected path | Latency | Detail |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var p in g.Probes)
            sb.AppendLine($"| {(p.Ok ? "✅" : "❌")} | {p.Name} | {p.Kind} {p.Target} | {p.ExpectVia ?? "-"} | {p.LatencyMs} ms | {p.Detail} |");
        sb.AppendLine();
        sb.AppendLine("> _\"Expected path\" is a label from config (your expectation). This tool checks TCP/DNS");
        sb.AppendLine("> reachability only — it does not measure whether traffic actually went via ZTNA, the VPN");
        sb.AppendLine("> tunnel, or the local network._");
        sb.AppendLine();
    }
}
