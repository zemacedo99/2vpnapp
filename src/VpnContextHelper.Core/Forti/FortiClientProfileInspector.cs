using VpnContextHelper.Core.SystemAccess;

namespace VpnContextHelper.Core.Forti;

/// <summary>
/// Read-only discovery of configured FortiClient VPN profiles and their protocol, by listing the
/// NAMES of the tunnel subkeys under the SSL-VPN and IPsec registry containers via <c>reg query</c>.
/// It never reads tunnel values (which may hold secrets) — only the container's direct child names.
/// </summary>
public sealed class FortiClientProfileInspector
{
    public const string SslKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Fortinet\FortiClient\Sslvpn\Tunnels";
    public const string IpsecKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Fortinet\FortiClient\IPSecVPN\Tunnels";

    private readonly ICommandRunner _runner;

    public FortiClientProfileInspector(ICommandRunner? runner = null)
        => _runner = runner ?? new ProcessCommandRunner();

    public IReadOnlyList<FortiVpnProfile> GetProfiles()
    {
        var profiles = new List<FortiVpnProfile>();

        var ssl = _runner.Run("reg.exe", $"query \"{SslKey}\"");
        foreach (var name in RegQueryParser.ParseDirectSubkeyNames(ssl.StdOut, SslKey))
            profiles.Add(new FortiVpnProfile(name, VpnProtocol.SslVpn));

        var ipsec = _runner.Run("reg.exe", $"query \"{IpsecKey}\"");
        foreach (var name in RegQueryParser.ParseDirectSubkeyNames(ipsec.StdOut, IpsecKey))
            profiles.Add(new FortiVpnProfile(name, VpnProtocol.IpsecVpn, IkeVersion: null));

        return profiles;
    }

    public int SslCount(IEnumerable<FortiVpnProfile> profiles) => profiles.Count(p => p.Protocol == VpnProtocol.SslVpn);
    public int IpsecCount(IEnumerable<FortiVpnProfile> profiles) => profiles.Count(p => p.Protocol == VpnProtocol.IpsecVpn);
}
