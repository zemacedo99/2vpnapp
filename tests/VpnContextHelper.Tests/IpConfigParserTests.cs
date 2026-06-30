using VpnContextHelper.Core.Net;
using Xunit;

namespace VpnContextHelper.Tests;

public class IpConfigParserTests
{
    [Fact]
    public void ParsesAllAdapters_ExcludingHeaderBlock()
    {
        var adapters = IpConfigParser.Parse(TestFixtures.IpConfigAll);
        Assert.Equal(3, adapters.Count);
        Assert.Equal(new[] { "Ethernet 3", "Ethernet", "WiFi" }, adapters.Select(a => a.Name).ToArray());
    }

    [Fact]
    public void DetectsFortinetAdapterAndItsIPv4()
    {
        var adapters = IpConfigParser.Parse(TestFixtures.IpConfigAll);
        var forti = Assert.Single(adapters, a => a.IsFortinet);
        Assert.Equal("Ethernet 3", forti.Name);
        Assert.Equal("10.20.0.2", forti.IPv4!.ToString());
        Assert.False(forti.MediaDisconnected);
    }

    [Fact]
    public void DetectsDisconnectedAdapterWithNoIPv4()
    {
        var adapters = IpConfigParser.Parse(TestFixtures.IpConfigAll);
        var eth = Assert.Single(adapters, a => a.Name == "Ethernet");
        Assert.True(eth.MediaDisconnected);
        Assert.Null(eth.IPv4);
        Assert.False(eth.IsFortinet);
    }

    [Fact]
    public void NonFortinetAdapterWithIPv4()
    {
        var adapters = IpConfigParser.Parse(TestFixtures.IpConfigAll);
        var wifi = Assert.Single(adapters, a => a.Name == "WiFi");
        Assert.Equal("192.168.1.10", wifi.IPv4!.ToString());
        Assert.False(wifi.IsFortinet);
    }
}
