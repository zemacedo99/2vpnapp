using VpnContextHelper.Core.Forti;
using VpnContextHelper.Core.Probes;

namespace VpnContextHelper.Core.Diagnostics;

/// <summary>Classification of why a resource is unreachable.</summary>
public enum ResourceDiagnosisClassification
{
    Reachable,
    ZtnaProxyNotRunning,
    ZtnaListenersMissing,
    DnsFailure,
    TcpFailure,
    Unknown,
}

/// <summary>
/// Read-only diagnostic result for a single named resource.
/// Pure record — all logic is in the pure methods; no I/O.
/// </summary>
public sealed record ResourceDiagnosis(
    string Name,
    string Host,
    int Port,
    string Category,
    ZtnaAgentState AgentState,
    ProbeResult DnsResult,
    ProbeResult? TcpResult)
{
    public ResourceDiagnosisClassification Classify()
    {
        if (!AgentState.ProxyRunning)
            return ResourceDiagnosisClassification.ZtnaProxyNotRunning;
        if (AgentState.ActiveListenerPorts.Count == 0)
            return ResourceDiagnosisClassification.ZtnaListenersMissing;
        if (!DnsResult.Ok)
            return ResourceDiagnosisClassification.DnsFailure;
        if (TcpResult is not null && !TcpResult.Ok)
            return ResourceDiagnosisClassification.TcpFailure;
        if (TcpResult is { Ok: true })
            return ResourceDiagnosisClassification.Reachable;
        return ResourceDiagnosisClassification.Unknown;
    }

    public string HumanReadableConclusion() => Classify() switch
    {
        ResourceDiagnosisClassification.ZtnaProxyNotRunning =>
            "ZTNA proxy not running — the FortiClient ZTNA access proxy (FortiProxy) is not " +
            "active. The FortiClient UI may show 'Zero Trust Telemetry: Connected' — this reflects " +
            "the EMS management-plane connection (FortiESNAC), NOT the ZTNA data-plane proxy " +
            "(FortiProxy). Resources served via ZTNA are unreachable while FortiProxy is not running.",

        ResourceDiagnosisClassification.ZtnaListenersMissing =>
            "ZTNA proxy running but no listeners — FortiProxy is running but the expected ZTNA " +
            "proxy listener ports (999, 17713–17715) are not open. ZTNA interception is not active. " +
            "The ZTNA policy may not have been applied on this network.",

        ResourceDiagnosisClassification.DnsFailure =>
            $"DNS failure — '{Host}' did not resolve to any address via the current DNS path. " +
            "If the client VPN is overriding all interface DNS servers with its own resolvers, " +
            "company internal names cannot be resolved (client VPN resolvers don't know them). " +
            "The ZTNA proxy (when running) provides DNS interception that works around this.",

        ResourceDiagnosisClassification.TcpFailure =>
            $"TCP failure — DNS resolved but port {Port} is unreachable at '{Host}'. " +
            "Check routing, ZTNA rule, and firewall.",

        ResourceDiagnosisClassification.Reachable =>
            $"Reachable — '{Name}' answered on {Host}:{Port}.",

        _ => "Unknown state — could not determine the cause.",
    };

    public string NextSafeAction() => Classify() switch
    {
        ResourceDiagnosisClassification.ZtnaProxyNotRunning =>
            "In the FortiClient UI → 'Zero Trust Telemetry' tab → re-authenticate or reconnect. " +
            "Check that the endpoint posture/compliance is green. " +
            "Then re-run 'vpnhelper diagnose-resource' to verify FortiProxy starts.",

        ResourceDiagnosisClassification.ZtnaListenersMissing =>
            "FortiProxy is running but ZTNA policy may not be applied on this network. " +
            "Try re-authenticating Zero Trust in the FortiClient UI → 'Zero Trust Telemetry' tab. " +
            "Contact company IT if the issue persists after re-authentication.",

        ResourceDiagnosisClassification.DnsFailure =>
            "Check whether the client VPN is pushing its DNS to all interfaces (see DNS servers " +
            "in the output above). If so, the ZTNA proxy (FortiProxy) resolves the company name " +
            "via its gateway — get FortiProxy running first (re-authenticate Zero Trust). " +
            "Do NOT add manual DNS entries or change DNS settings.",

        ResourceDiagnosisClassification.TcpFailure =>
            $"DNS resolved but TCP to {Host}:{Port} failed. " +
            "Check whether the ZTNA rule for this resource is published. " +
            "Run 'vpnhelper it-request' to request IT to publish it as a ZTNA application.",

        ResourceDiagnosisClassification.Reachable =>
            "No action needed — resource is reachable.",

        _ => "Re-run with 'vpnhelper report' to generate a full diagnostic for IT.",
    };
}
