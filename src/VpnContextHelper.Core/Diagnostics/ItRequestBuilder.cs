using System.Text;
using VpnContextHelper.Core.Config;
using VpnContextHelper.Core.Context;

namespace VpnContextHelper.Core.Diagnostics;

/// <summary>
/// Generates a ready-to-send IT request that asks for the SUPPORTED fix (extend ZTNA coverage,
/// or approve a VM-isolation workflow) and embeds the redacted evidence. It never asks IT to
/// weaken any control.
/// </summary>
public static class ItRequestBuilder
{
    public static string Build(AppConfig config, DiagnosticData data, NetworkContext ctx, Redactor redactor)
    {
        var sb = new StringBuilder();
        var company = config.CompanyAccess.Label;
        var client = config.ClientAccess.Label;

        static string Describe(Probes.ProbeResult p)
            => p.ExpectVia is null ? $"{p.Name} ({p.Target})" : $"{p.Name} ({p.Target}) — expected via {p.ExpectVia}";

        var unreachableCompany = ctx.Company.Unreachable.Select(Describe).ToList();
        var reachableCompany = ctx.Company.Probes.Where(p => p.Ok).Select(Describe).ToList();

        sb.AppendLine("# Support request: simultaneous access to company and client resources");
        sb.AppendLine();
        sb.AppendLine($"**Requested by:** (your name)  ");
        sb.AppendLine($"**Date:** {data.GeneratedAt:yyyy-MM-dd}  ");
        sb.AppendLine($"**Machine:** {data.MachineName}  ");
        sb.AppendLine($"**FortiClient:** {data.FortiClientVersion}");
        sb.AppendLine();

        sb.AppendLine("## What I'm trying to do");
        sb.AppendLine();
        sb.AppendLine($"I need to use **{company}** resources (e.g. Bitbucket and internal tools) and **{client}** ");
        sb.AppendLine($"resources (reached over the `{config.ClientAccess.VpnProfileName ?? client}` FortiClient VPN) ");
        sb.AppendLine("during the same working session, without repeatedly disconnecting one VPN to use the other.");
        sb.AppendLine();
        sb.AppendLine("I am **not** asking to bypass, disable or weaken any security control. I'm asking for a ");
        sb.AppendLine("supported configuration that lets both access paths coexist.");
        sb.AppendLine();

        sb.AppendLine("## What already works (no change needed)");
        sb.AppendLine();
        if (reachableCompany.Count > 0)
        {
            sb.AppendLine($"While the client VPN is connected, these **{company}** resources are already reachable ");
            sb.AppendLine("at the same time (the *expected* path is shown per item; these addresses are outside the ");
            sb.AppendLine("client tunnel's routed ranges, consistent with ZTNA access-proxy or cloud delivery — please ");
            sb.AppendLine("confirm which):");
            sb.AppendLine();
            foreach (var r in reachableCompany) sb.AppendLine($"- ✅ {r}");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("_(Run the helper with the client VPN connected to populate this section.)_");
            sb.AppendLine();
        }

        sb.AppendLine("## What does not work (the ask)");
        sb.AppendLine();
        if (unreachableCompany.Count > 0)
        {
            sb.AppendLine($"With the client VPN connected, these **{company}** resources are **not** reachable:");
            sb.AppendLine();
            foreach (var r in unreachableCompany) sb.AppendLine($"- ❌ {r}");
            sb.AppendLine();
            sb.AppendLine("**Preferred fix (Option B):** please publish the above resources as **ZTNA applications** ");
            sb.AppendLine("(FortiGate ZTNA access proxy / TCP forwarding, EMS-managed), as appears to already be done ");
            sb.AppendLine("for the resources in the previous section. ZTNA reaches internal apps without a full VPN ");
            sb.AppendLine("tunnel, so it coexists with the client VPN and removes the need to switch.");
        }
        else
        {
            sb.AppendLine("At the time this request was generated, all configured company resources were reachable ");
            sb.AppendLine("alongside the client VPN. If specific tools still require the full company VPN, list them here.");
        }
        sb.AppendLine();

        if (ctx.RouteOverlaps.Count > 0)
        {
            sb.AppendLine("## Routing/addressing overlap to be aware of");
            sb.AppendLine();
            sb.AppendLine("The client VPN's split-tunnel routes capture the following company ranges, which can ");
            sb.AppendLine("break those company resources while the client VPN is up:");
            sb.AppendLine();
            foreach (var (t, c) in ctx.RouteOverlaps)
                sb.AppendLine($"- company `{c}` is inside client tunnel range `{t}`");
            sb.AppendLine();
        }

        sb.AppendLine("## Alternatives if ZTNA coverage cannot be extended");
        sb.AppendLine();
        sb.AppendLine("- **Option C — VM isolation (no server change):** I run the client VPN inside a NAT'd ");
        sb.AppendLine("  virtual machine while the host keeps the company context. Please confirm this is acceptable ");
        sb.AppendLine("  under both company and client policy (the client VPN may enforce device posture/compliance ");
        sb.AppendLine("  on the endpoint). See `docs/vm-isolation-workflow.md`.");
        sb.AppendLine("- **Dual-tunnel (server-side):** FortiClient 7.4.4+ supports concurrent **IPsec IKEv2** ");
        sb.AppendLine("  tunnels via *Multi-Connect* for EMS-managed profiles — but this is an EMS/profile change ");
        sb.AppendLine("  on your side and does not cover SSL-VPN or cross-organization profiles, so ZTNA is likely simpler.");
        sb.AppendLine();

        sb.AppendLine("## Evidence");
        sb.AppendLine();
        sb.AppendLine($"- OS: {data.OsCaption} ({data.OsVersion})");
        sb.AppendLine($"- Active tunnel: {(ctx.Vpn.Connected ? $"yes, {(ctx.Vpn.IsFullTunnel ? "full" : "split")} tunnel, IP {ctx.Vpn.TunnelIp}" : "none")}");
        if (ctx.Vpn.TunneledCidrs.Count > 0)
            sb.AppendLine($"- Tunneled ranges: {string.Join(", ", ctx.Vpn.TunneledCidrs.Select(c => c.ToString()))}");
        sb.AppendLine();
        sb.AppendLine("A full read-only diagnostic report (routes, adapters, reachability — secrets redacted) is ");
        sb.AppendLine("attached separately; generate it with `vpnhelper report`.");
        sb.AppendLine();

        return redactor.Redact(sb.ToString());
    }
}
