using VpnContextHelper.Core.Forti;
using VpnContextHelper.Core.SystemAccess;
using Xunit;

namespace VpnContextHelper.Tests;

/// <summary>Fake command runner returning canned `reg query` output (no live registry access).</summary>
internal sealed class FakeRegRunner : ICommandRunner
{
    private readonly string _sslOut;
    private readonly string _ipsecOut;

    public FakeRegRunner(string sslOut, string ipsecOut)
    {
        _sslOut = sslOut;
        _ipsecOut = ipsecOut;
    }

    public CommandResult Run(string fileName, string arguments, int timeoutMs = 15000)
    {
        if (arguments.Contains("Sslvpn", StringComparison.OrdinalIgnoreCase))
            return new CommandResult(0, _sslOut, string.Empty);
        if (arguments.Contains("IPSecVPN", StringComparison.OrdinalIgnoreCase))
            return new CommandResult(0, _ipsecOut, string.Empty);
        return new CommandResult(1, string.Empty, "unexpected key");
    }
}

public class FortiProfileInspectionTests
{
    // Generic profile names (no real names committed). Mirrors real `reg query` layout: the queried
    // key with its values (indented), then each subkey as a full path line.
    private const string SslRegOutput =
        "HKEY_LOCAL_MACHINE\\SOFTWARE\\Fortinet\\FortiClient\\Sslvpn\\Tunnels\r\n" +
        "    (Default)    REG_SZ    \r\n" +
        "\r\n" +
        "HKEY_LOCAL_MACHINE\\SOFTWARE\\Fortinet\\FortiClient\\Sslvpn\\Tunnels\\Company VPN\r\n" +
        "HKEY_LOCAL_MACHINE\\SOFTWARE\\Fortinet\\FortiClient\\Sslvpn\\Tunnels\\Company VPN Full\r\n" +
        "HKEY_LOCAL_MACHINE\\SOFTWARE\\Fortinet\\FortiClient\\Sslvpn\\Tunnels\\Client VPN\r\n";

    private const string EmptyIpsecOutput =
        "HKEY_LOCAL_MACHINE\\SOFTWARE\\Fortinet\\FortiClient\\IPSecVPN\\Tunnels\r\n";

    [Fact]
    public void RegQueryParser_ExtractsDirectSubkeyNames_IncludingSpaces()
    {
        var names = RegQueryParser.ParseDirectSubkeyNames(SslRegOutput, FortiClientProfileInspector.SslKey);
        Assert.Equal(new[] { "Company VPN", "Company VPN Full", "Client VPN" }, names.ToArray());
    }

    [Fact]
    public void RegQueryParser_IgnoresValuesAndDeeperKeys()
    {
        var withDeep = SslRegOutput +
            "HKEY_LOCAL_MACHINE\\SOFTWARE\\Fortinet\\FortiClient\\Sslvpn\\Tunnels\\Client VPN\\SubThing\r\n";
        var names = RegQueryParser.ParseDirectSubkeyNames(withDeep, FortiClientProfileInspector.SslKey);
        Assert.DoesNotContain("Client VPN\\SubThing", names);
        Assert.Equal(3, names.Count); // deeper key not double-counted
    }

    [Fact]
    public void Inspector_ClassifiesAllAsSslVpn_WhenNoIpsecTunnels()
    {
        var inspector = new FortiClientProfileInspector(new FakeRegRunner(SslRegOutput, EmptyIpsecOutput));
        var profiles = inspector.GetProfiles();

        Assert.Equal(3, inspector.SslCount(profiles));
        Assert.Equal(0, inspector.IpsecCount(profiles));
        Assert.All(profiles, p => Assert.Equal(VpnProtocol.SslVpn, p.Protocol));

        // The decisive real-world condition: two SSL-VPN profiles => host-only dual is blocked.
        var company = profiles[0];
        var client = profiles[^1];
        var decision = DualConnectFeasibility.Evaluate(company, client);
        Assert.False(decision.HostOnlyDualPossible);
        Assert.Equal(DualConnectVerdict.BlockedSslVpnSingleConnection, decision.Verdict);
    }

    [Fact]
    public void Inspector_DetectsIpsecTunnels_WhenPresent()
    {
        var ipsec =
            "HKEY_LOCAL_MACHINE\\SOFTWARE\\Fortinet\\FortiClient\\IPSecVPN\\Tunnels\r\n" +
            "HKEY_LOCAL_MACHINE\\SOFTWARE\\Fortinet\\FortiClient\\IPSecVPN\\Tunnels\\Lab IPsec\r\n";
        var inspector = new FortiClientProfileInspector(new FakeRegRunner(EmptyIpsecOutput.Replace("IPSecVPN", "Sslvpn"), ipsec));
        var profiles = inspector.GetProfiles();
        Assert.Equal(1, inspector.IpsecCount(profiles));
        Assert.Contains(profiles, p => p.Protocol == VpnProtocol.IpsecVpn && p.Name == "Lab IPsec");
    }
}
