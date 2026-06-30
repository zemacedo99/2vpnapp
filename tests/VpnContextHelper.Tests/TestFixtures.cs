namespace VpnContextHelper.Tests;

/// <summary>
/// Synthetic `route print -4` / `ipconfig /all` text used as deterministic parser fixtures.
/// All addresses and MACs are placeholders (no live machine data); they exercise the same shapes
/// the real commands produce: a split tunnel on a Fortinet adapter with two /32 DNS routes.
/// </summary>
internal static class TestFixtures
{
    // Split tunnel active on a Fortinet adapter at 10.20.0.2.
    public const string RoutePrintSplitTunnel = """
        ===========================================================================
        Interface List
         10...02 00 5e 00 00 01 ......Fortinet SSL VPN Virtual Ethernet Adapter
         30...02 00 5e 00 00 03 ......Intel(R) Wi-Fi 6 AX201 160MHz
        ===========================================================================

        IPv4 Route Table
        ===========================================================================
        Active Routes:
        Network Destination        Netmask          Gateway       Interface  Metric
                  0.0.0.0          0.0.0.0      192.168.1.1      192.168.1.10     35
                10.20.0.2  255.255.255.255         On-link         10.20.0.2    257
               172.16.0.0      255.240.0.0        10.20.0.1         10.20.0.2      1
              172.16.4.11  255.255.255.255        10.20.0.1         10.20.0.2      1
              172.16.4.10  255.255.255.255        10.20.0.1         10.20.0.2      1
              192.168.1.0    255.255.255.0         On-link      192.168.1.10    291
                224.0.0.0        240.0.0.0         On-link         10.20.0.2    257
          255.255.255.255  255.255.255.255         On-link         10.20.0.2    257
        ===========================================================================
        Persistent Routes:
          Network Address          Netmask  Gateway Address  Metric
                  0.0.0.0          0.0.0.0      192.168.0.2  Default
        ===========================================================================
        """;

    // Same, but the tunnel carries the default route (full tunnel).
    public const string RoutePrintFullTunnel = """
        ===========================================================================
        Active Routes:
        Network Destination        Netmask          Gateway       Interface  Metric
                  0.0.0.0          0.0.0.0        10.20.0.1         10.20.0.2      1
                10.20.0.2  255.255.255.255         On-link         10.20.0.2    257
              192.168.1.0    255.255.255.0         On-link      192.168.1.10    291
        ===========================================================================
        """;

    public const string IpConfigAll = """
        Windows IP Configuration

           Host Name . . . . . . . . . . . . : TEST-PC
           Primary Dns Suffix  . . . . . . . : example.local

        Ethernet adapter Ethernet 3:

           Connection-specific DNS Suffix  . :
           Description . . . . . . . . . . . : Fortinet SSL VPN Virtual Ethernet Adapter
           Physical Address. . . . . . . . . : 02-00-5E-00-00-01
           DHCP Enabled. . . . . . . . . . . : No
           IPv4 Address. . . . . . . . . . . : 10.20.0.2(Preferred)
           Subnet Mask . . . . . . . . . . . : 255.255.255.255

        Ethernet adapter Ethernet:

           Media State . . . . . . . . . . . : Media disconnected
           Connection-specific DNS Suffix  . :
           Description . . . . . . . . . . . : Intel(R) Ethernet Connection (14) I219-V
           Physical Address. . . . . . . . . : 02-00-5E-00-00-02

        Wireless LAN adapter WiFi:

           Connection-specific DNS Suffix  . : Home
           Description . . . . . . . . . . . : Intel(R) Wi-Fi 6 AX201 160MHz
           Physical Address. . . . . . . . . : 02-00-5E-00-00-03
           IPv4 Address. . . . . . . . . . . : 192.168.1.10(Preferred)
           Subnet Mask . . . . . . . . . . . : 255.255.255.0
        """;
}
