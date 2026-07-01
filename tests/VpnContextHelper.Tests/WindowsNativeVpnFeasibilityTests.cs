using VpnContextHelper.Core.Forti;
using Xunit;

namespace VpnContextHelper.Tests;

public class WindowsNativeVpnFeasibilityTests
{
    [Fact]
    public void SslVpn_CannotRehost()
    {
        var d = WindowsNativeVpnFeasibility.Evaluate(new FortiVpnProfile("Company VPN", VpnProtocol.SslVpn));
        Assert.False(d.CanRehostOnNativeWindows);
        Assert.Contains(d.Reasons, r => r.Contains("SSL-VPN"));
    }

    [Fact]
    public void SamlExternalBrowser_CannotRehost_EvenIfIpsec()
    {
        var d = WindowsNativeVpnFeasibility.Evaluate(
            new FortiVpnProfile("Client VPN", VpnProtocol.IpsecVpn, IkeVersion: "2", UsesSamlExternalBrowser: true));
        Assert.False(d.CanRehostOnNativeWindows);
        Assert.Contains(d.Reasons, r => r.Contains("SAML"));
    }

    [Fact]
    public void IpsecIkev2_NoSaml_CanRehost_WithCaveat()
    {
        var d = WindowsNativeVpnFeasibility.Evaluate(
            new FortiVpnProfile("Lab VPN", VpnProtocol.IpsecVpn, IkeVersion: "2"));
        Assert.True(d.CanRehostOnNativeWindows);
        Assert.Contains(d.Reasons, r => r.Contains("NOT extract", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IpsecIkev1_CannotRehost()
    {
        var d = WindowsNativeVpnFeasibility.Evaluate(
            new FortiVpnProfile("Old VPN", VpnProtocol.IpsecVpn, IkeVersion: "1"));
        Assert.False(d.CanRehostOnNativeWindows);
    }
}
