using VpnContextHelper.Core.Config;
using VpnContextHelper.Core.Net;
using VpnContextHelper.Core.Probes;

namespace VpnContextHelper.Core.Context;

public enum AccessVerdict
{
    Reachable,
    Partial,
    Unreachable,
    Unknown,
}

public sealed record GroupStatus(string Label, AccessVerdict Verdict, IReadOnlyList<ProbeResult> Probes)
{
    public IEnumerable<ProbeResult> Unreachable => Probes.Where(p => !p.Ok);
}

/// <summary>The consolidated picture shown in the UI and the diagnostic report.</summary>
public sealed record NetworkContext(
    bool TrustedNetworkDetected,
    VpnState Vpn,
    GroupStatus Company,
    GroupStatus Client,
    IReadOnlyList<(Cidr Tunneled, Cidr Company)> RouteOverlaps,
    IReadOnlyList<string> Messages);

/// <summary>
/// Pure logic that turns probe results + VPN state into a human-readable verdict and guidance.
/// No I/O — the caller supplies already-gathered <see cref="ProbeResult"/>s.
/// </summary>
public static class NetworkContextEvaluator
{
    public static GroupStatus EvaluateGroup(string label, IReadOnlyList<ProbeResult> probes)
    {
        if (probes.Count == 0) return new GroupStatus(label, AccessVerdict.Unknown, probes);
        bool allOk = probes.All(p => p.Ok);
        bool anyOk = probes.Any(p => p.Ok);
        var verdict = allOk ? AccessVerdict.Reachable
            : anyOk ? AccessVerdict.Partial
            : AccessVerdict.Unreachable;
        return new GroupStatus(label, verdict, probes);
    }

    public static NetworkContext Build(
        AppConfig config,
        bool trusted,
        VpnState vpn,
        IReadOnlyList<ProbeResult> companyProbes,
        IReadOnlyList<ProbeResult> clientProbes)
    {
        var company = EvaluateGroup(config.CompanyAccess.Label, companyProbes);
        var client = EvaluateGroup(config.ClientAccess.Label, clientProbes);

        var companyInternal = config.CompanyAccess.InternalCidrs
            .Select(s => Cidr.TryParse(s, out var c) ? (Cidr?)c : null)
            .Where(c => c.HasValue)
            .Select(c => c!.Value)
            .ToList();
        // Exclude the default route (/0): a full tunnel "capturing everything" is expected, not a
        // conflict, so it must not raise a spurious overlap warning.
        var tunneledSpecific = vpn.TunneledCidrs.Where(c => c.PrefixLength != 0);
        var overlaps = CidrTools.FindOverlaps(tunneledSpecific, companyInternal);

        var messages = new List<string>();

        if (trusted)
            messages.Add($"Trusted network detected — {config.CompanyAccess.Label} resources should be reachable without the company VPN (Zero Trust on-net).");

        messages.Add(vpn.Connected
            ? $"FortiClient tunnel active (IP {vpn.TunnelIp}, {(vpn.IsFullTunnel ? "FULL tunnel" : "SPLIT tunnel")}, {vpn.TunneledCidrs.Count} routed range(s))."
            : "No FortiClient VPN tunnel currently active.");

        AppendGroupMessage(messages, company);
        AppendGroupMessage(messages, client);

        if (overlaps.Count > 0)
        {
            var pairs = string.Join("; ", overlaps.Select(o => $"{o.Company} ⊂ tunnel {o.Tunneled}"));
            messages.Add($"⚠ Route overlap: client tunnel captures company range(s): {pairs}. This can break those company resources while the client VPN is up — raise with IT.");
        }

        // "Do I need to switch?" guidance
        bool companyBroken = company.Verdict is AccessVerdict.Unreachable or AccessVerdict.Partial;
        bool clientBroken = client.Verdict is AccessVerdict.Unreachable or AccessVerdict.Partial;

        if (companyBroken && !clientBroken && vpn.Connected)
            messages.Add($"Guidance: client access works; some {config.CompanyAccess.Label} resources are NOT reachable. Preferred fix is to have IT publish them as ZTNA applications so no switching is needed (see docs/architecture.md). Otherwise you must switch to the company VPN or use the VM workflow.");
        else if (clientBroken && !companyBroken)
            messages.Add($"Guidance: {config.CompanyAccess.Label} access works; connect the '{config.ClientAccess.VpnProfileName ?? config.ClientAccess.Label}' VPN in FortiClient for client resources.");
        else if (!companyBroken && !clientBroken && companyProbes.Count > 0 && clientProbes.Count > 0)
            messages.Add("Guidance: both company and client resources are reachable right now — no switching needed.");

        return new NetworkContext(trusted, vpn, company, client, overlaps, messages);
    }

    private static void AppendGroupMessage(List<string> messages, GroupStatus g)
    {
        switch (g.Verdict)
        {
            case AccessVerdict.Reachable:
                messages.Add($"{g.Label}: all checked resources reachable.");
                break;
            case AccessVerdict.Partial:
                messages.Add($"{g.Label}: PARTIAL — unreachable: {string.Join(", ", g.Unreachable.Select(p => p.Name))}.");
                break;
            case AccessVerdict.Unreachable:
                messages.Add($"{g.Label}: NOT reachable — {string.Join(", ", g.Unreachable.Select(p => p.Name))}.");
                break;
            case AccessVerdict.Unknown:
            default:
                break;
        }
    }
}
