namespace VpnContextHelper.Core.Forti;

public enum VpnProtocol
{
    SslVpn,
    IpsecVpn,
    Unknown,
}

/// <summary>
/// A configured FortiClient VPN profile, described only by non-secret metadata (name + type +
/// capability flags). No credentials, PSKs, certificates or gateways are represented here.
/// </summary>
public sealed record FortiVpnProfile(
    string Name,
    VpnProtocol Protocol,
    bool EmsManaged = false,
    string? EmsAuthority = null,           // logical EMS/org that manages it; different values => cross-org
    string? IkeVersion = null,             // "1" | "2" | null (SSL-VPN)
    bool UsesSamlExternalBrowser = false,
    bool MultiConnectEligible = false);    // EMS "Enable Multi-Connect on eligible tunnels"
