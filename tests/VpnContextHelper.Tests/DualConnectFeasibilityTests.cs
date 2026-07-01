using VpnContextHelper.Core.Forti;
using Xunit;

namespace VpnContextHelper.Tests;

public class DualConnectFeasibilityTests
{
    private static FortiVpnProfile Ssl(string n) => new(n, VpnProtocol.SslVpn);

    private static FortiVpnProfile Ipsec(string n, string ike = "2", bool ems = true,
        string? authority = "org", bool multi = true) =>
        new(n, VpnProtocol.IpsecVpn, EmsManaged: ems, EmsAuthority: authority, IkeVersion: ike, MultiConnectEligible: multi);

    [Fact]
    public void TwoSslVpn_Blocked_SingleConnection()
    {
        var d = DualConnectFeasibility.Evaluate(Ssl("Company VPN"), Ssl("Client VPN"));
        Assert.Equal(DualConnectVerdict.BlockedSslVpnSingleConnection, d.Verdict);
        Assert.False(d.HostOnlyDualPossible);
        Assert.Contains(d.Reasons, r => r.Contains("SSL-VPN"));
        Assert.Contains(d.Reasons, r => r.Contains("Multi-Connect") && r.Contains("IPsec"));
        Assert.NotEmpty(d.WhoMustChange);
    }

    [Fact]
    public void MixedSslIpsec_Blocked()
    {
        var d = DualConnectFeasibility.Evaluate(Ssl("Company VPN"), Ipsec("Client VPN"));
        Assert.Equal(DualConnectVerdict.BlockedMixedSslIpsec, d.Verdict);
        Assert.False(d.HostOnlyDualPossible);
    }

    [Fact]
    public void TwoIpsecIkev2_SameEms_MultiConnect_Supported()
    {
        var d = DualConnectFeasibility.Evaluate(
            Ipsec("Company VPN", authority: "acme"), Ipsec("Client VPN", authority: "acme"));
        Assert.Equal(DualConnectVerdict.SupportedMultiConnect, d.Verdict);
        Assert.True(d.HostOnlyDualPossible);
    }

    [Fact]
    public void TwoIpsecIkev2_DifferentEms_BlockedCrossOrg()
    {
        var d = DualConnectFeasibility.Evaluate(
            Ipsec("Company VPN", authority: "acme"), Ipsec("Client VPN", authority: "client-corp"));
        Assert.Equal(DualConnectVerdict.BlockedCrossOrgEms, d.Verdict);
        Assert.False(d.HostOnlyDualPossible);
    }

    [Fact]
    public void TwoIpsecIkev2_SameEms_NotEligible_Blocked()
    {
        var d = DualConnectFeasibility.Evaluate(
            Ipsec("Company VPN", authority: "acme", multi: false),
            Ipsec("Client VPN", authority: "acme", multi: false));
        Assert.Equal(DualConnectVerdict.BlockedMultiConnectNotEnabled, d.Verdict);
        Assert.False(d.HostOnlyDualPossible);
    }

    [Fact]
    public void Ipsec_NotIkev2_Blocked()
    {
        var d = DualConnectFeasibility.Evaluate(Ipsec("A", ike: "1"), Ipsec("B", ike: "2"));
        Assert.Equal(DualConnectVerdict.BlockedMultiConnectNotEnabled, d.Verdict);
    }

    [Fact]
    public void UnknownProtocol_Unknown()
    {
        var d = DualConnectFeasibility.Evaluate(new FortiVpnProfile("x", VpnProtocol.Unknown), Ssl("y"));
        Assert.Equal(DualConnectVerdict.Unknown, d.Verdict);
        Assert.False(d.HostOnlyDualPossible);
    }
}
