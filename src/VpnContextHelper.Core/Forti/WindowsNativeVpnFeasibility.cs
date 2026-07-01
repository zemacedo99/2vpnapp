namespace VpnContextHelper.Core.Forti;

public sealed record NativeVpnDecision(bool CanRehostOnNativeWindows, IReadOnlyList<string> Reasons);

/// <summary>
/// Pure logic for "could this FortiClient profile instead run on the built-in Windows VPN client?"
/// The native client supports only IKEv2/L2TP/SSTP/PPTP with EAP/certificate/PSK — no SSL-VPN,
/// no SAML/external-browser SSO. This never involves extracting any secret; it only judges whether
/// the auth/protocol model is even expressible natively.
/// </summary>
public static class WindowsNativeVpnFeasibility
{
    public static NativeVpnDecision Evaluate(FortiVpnProfile p)
    {
        if (p.Protocol == VpnProtocol.SslVpn)
        {
            return new NativeVpnDecision(false, new[]
            {
                "Profile is SSL-VPN; Windows has no built-in SSL-VPN client, so it cannot connect a FortiGate SSL-VPN.",
            });
        }

        if (p.UsesSamlExternalBrowser)
        {
            return new NativeVpnDecision(false, new[]
            {
                "Profile uses SAML SSO via an external browser, which is a FortiClient-specific flow the native Windows client cannot perform.",
            });
        }

        if (p.Protocol == VpnProtocol.IpsecVpn && p.IkeVersion == "2")
        {
            return new NativeVpnDecision(true, new[]
            {
                "IPsec IKEv2 without SAML can, in principle, be expressed on the native Windows client using certificate or EAP auth.",
                "CAVEAT: this requires IT to provide a native-compatible connection profile — the app must NOT extract config/secrets from FortiClient.",
                "It also does not change that both tunnels' auth (MFA/cert) must be satisfied interactively.",
            });
        }

        return new NativeVpnDecision(false, new[]
        {
            "Protocol/auth model is not expressible on the built-in Windows VPN client without changes from IT.",
        });
    }
}
