namespace VpnContextHelper.Core.Forti;

public enum DualConnectVerdict
{
    /// <summary>Both tunnels can be held at once via the supported Multi-Connect feature.</summary>
    SupportedMultiConnect,

    /// <summary>Two SSL-VPN profiles — FortiClient allows only one SSL-VPN connection at a time.</summary>
    BlockedSslVpnSingleConnection,

    /// <summary>SSL-VPN + IPsec mix — not a supported concurrent combination.</summary>
    BlockedMixedSslIpsec,

    /// <summary>Both IPsec IKEv2 but managed by different EMS/organisations — Multi-Connect is per EMS profile.</summary>
    BlockedCrossOrgEms,

    /// <summary>Both IPsec but not IKEv2 and/or Multi-Connect not enabled — a supported change could unblock.</summary>
    BlockedMultiConnectNotEnabled,

    Unknown,
}

public sealed record DualConnectDecision(
    DualConnectVerdict Verdict,
    bool HostOnlyDualPossible,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> WhatWouldNeedToChange,
    IReadOnlyList<string> WhoMustChange);

/// <summary>
/// Pure decision logic for "can these two FortiClient profiles be connected at the same time on
/// the host?" Encodes the documented Fortinet rules: SSL-VPN is single-connection; the only
/// concurrent-tunnel feature (Multi-Connect, 7.4.4+) is EMS-managed IPsec IKEv2 only. No secrets.
/// </summary>
public static class DualConnectFeasibility
{
    public static DualConnectDecision Evaluate(FortiVpnProfile company, FortiVpnProfile client)
    {
        if (company.Protocol == VpnProtocol.Unknown || client.Protocol == VpnProtocol.Unknown)
        {
            return new DualConnectDecision(
                DualConnectVerdict.Unknown, false,
                new[] { "One or both profile types could not be determined." },
                new[] { "Re-run detection with FortiClient installed and profiles configured." },
                Array.Empty<string>());
        }

        bool bothSsl = company.Protocol == VpnProtocol.SslVpn && client.Protocol == VpnProtocol.SslVpn;
        bool bothIpsec = company.Protocol == VpnProtocol.IpsecVpn && client.Protocol == VpnProtocol.IpsecVpn;
        bool mixed = !bothSsl && !bothIpsec;

        if (bothSsl)
        {
            return new DualConnectDecision(
                DualConnectVerdict.BlockedSslVpnSingleConnection,
                HostOnlyDualPossible: false,
                Reasons: new[]
                {
                    "Both profiles are SSL-VPN.",
                    "FortiClient connects only one SSL-VPN tunnel at a time; all SSL-VPN profiles share a single 'Fortinet SSL VPN Virtual Ethernet Adapter'.",
                    "The Multi-Connect concurrent-tunnel feature (FortiClient 7.4.4+) applies only to EMS-managed IPsec IKEv2 tunnels — it does not enable two SSL-VPN tunnels.",
                },
                WhatWouldNeedToChange: new[]
                {
                    "Both VPNs would have to be re-provisioned server-side as IPsec IKEv2 (not SSL-VPN),",
                    "managed by a single EMS with 'Enable Multi-Connect on eligible tunnels' turned on.",
                    "This is a server/EMS re-architecture, not something a host-only app can do.",
                },
                WhoMustChange: new[] { "Company IT (VPN gateway + EMS)", "Client IT (VPN gateway + EMS)" });
        }

        if (mixed)
        {
            return new DualConnectDecision(
                DualConnectVerdict.BlockedMixedSslIpsec,
                HostOnlyDualPossible: false,
                Reasons: new[]
                {
                    "One profile is SSL-VPN and the other is IPsec.",
                    "Fortinet does not document holding an SSL-VPN tunnel and an IPsec tunnel concurrently; Multi-Connect covers IPsec IKEv2 only.",
                },
                WhatWouldNeedToChange: new[]
                {
                    "The SSL-VPN side would need re-provisioning as EMS-managed IPsec IKEv2, alongside the IPsec side, with Multi-Connect enabled.",
                },
                WhoMustChange: new[] { "The IT team that owns the SSL-VPN profile", "possibly both, to share one EMS" });
        }

        // both IPsec
        bool bothIkev2 = company.IkeVersion == "2" && client.IkeVersion == "2";
        if (!bothIkev2)
        {
            return new DualConnectDecision(
                DualConnectVerdict.BlockedMultiConnectNotEnabled,
                HostOnlyDualPossible: false,
                Reasons: new[] { "Multi-Connect requires IPsec IKEv2 for both tunnels; at least one is not IKEv2." },
                WhatWouldNeedToChange: new[] { "Reconfigure both tunnels as IPsec IKEv2 and enable Multi-Connect in EMS." },
                WhoMustChange: new[] { "Company IT", "Client IT" });
        }

        bool sameEms = company.EmsManaged && client.EmsManaged &&
                       company.EmsAuthority is not null &&
                       string.Equals(company.EmsAuthority, client.EmsAuthority, StringComparison.OrdinalIgnoreCase);
        if (!sameEms)
        {
            return new DualConnectDecision(
                DualConnectVerdict.BlockedCrossOrgEms,
                HostOnlyDualPossible: false,
                Reasons: new[]
                {
                    "Both are IPsec IKEv2, but Multi-Connect groups tunnels within a single EMS-managed profile.",
                    "Two different organisations' EMS cannot be combined into one Multi-Connect profile on one endpoint.",
                },
                WhatWouldNeedToChange: new[] { "A single EMS would need to manage both tunnels in one profile with Multi-Connect enabled." },
                WhoMustChange: new[] { "Both organisations (agree on one EMS authority) — generally impractical across companies" });
        }

        if (!(company.MultiConnectEligible && client.MultiConnectEligible))
        {
            return new DualConnectDecision(
                DualConnectVerdict.BlockedMultiConnectNotEnabled,
                HostOnlyDualPossible: false,
                Reasons: new[] { "Both are IPsec IKEv2 under one EMS, but Multi-Connect is not enabled for both tunnels." },
                WhatWouldNeedToChange: new[] { "Enable 'Multi-Connect on eligible tunnels' in the EMS VPN profile." },
                WhoMustChange: new[] { "The EMS administrator" });
        }

        return new DualConnectDecision(
            DualConnectVerdict.SupportedMultiConnect,
            HostOnlyDualPossible: true,
            Reasons: new[] { "Both are EMS-managed IPsec IKEv2 tunnels with Multi-Connect enabled — a supported concurrent configuration." },
            WhatWouldNeedToChange: Array.Empty<string>(),
            WhoMustChange: Array.Empty<string>());
    }
}
