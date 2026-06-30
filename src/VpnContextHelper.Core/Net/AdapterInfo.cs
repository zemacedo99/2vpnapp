using System.Net;

namespace VpnContextHelper.Core.Net;

/// <summary>A network adapter as seen by the helper (parsed from ipconfig or read live).</summary>
public sealed record AdapterInfo(
    string Name,
    string Description,
    IPAddress? IPv4,
    string? MacAddress,
    bool MediaDisconnected)
{
    public bool IsFortinet =>
        Description.Contains("Fortinet", StringComparison.OrdinalIgnoreCase) ||
        Description.Contains("FortiSSL", StringComparison.OrdinalIgnoreCase);

    public bool IsVpnVirtual =>
        IsFortinet ||
        Description.Contains("VPN", StringComparison.OrdinalIgnoreCase) ||
        Description.Contains("TAP-Windows", StringComparison.OrdinalIgnoreCase) ||
        Description.Contains("WireGuard", StringComparison.OrdinalIgnoreCase);

    public bool HasIPv4 => IPv4 is not null;

    /// <summary>APIPA / link-local auto-config address (169.254.x.x) — not routable, treated as "no IP".</summary>
    public bool IsApipa => IPv4 is not null && IPv4.ToString().StartsWith("169.254", StringComparison.Ordinal);

    public bool HasRoutableIPv4 => IPv4 is not null && !IsApipa;

    /// <summary>NDIS filter / scheduler pseudo-adapters that clutter enumeration but carry no IP.</summary>
    public bool IsNdisFilterPseudo =>
        Description.Contains("LightWeight Filter", StringComparison.OrdinalIgnoreCase) ||
        Description.Contains("QoS Packet Scheduler", StringComparison.OrdinalIgnoreCase) ||
        Description.Contains("Native MAC Layer", StringComparison.OrdinalIgnoreCase) ||
        Description.Contains("Juniper Network Service", StringComparison.OrdinalIgnoreCase) ||
        Name.EndsWith("-0000", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether to surface this adapter in the (summarised) diagnostic table.</summary>
    public bool IsInteresting =>
        !IsNdisFilterPseudo && (IsFortinet || HasRoutableIPv4 || (IsVpnVirtual && !MediaDisconnected));
}
