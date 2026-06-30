using System.Net;
using VpnContextHelper.Core.Net;
using Xunit;

namespace VpnContextHelper.Tests;

public class CidrTests
{
    [Theory]
    [InlineData("172.16.0.0/12", "172.16.0.0", 12)]
    [InlineData("10.40.0.10/32", "10.40.0.10", 32)]
    [InlineData("0.0.0.0/0", "0.0.0.0", 0)]
    [InlineData("192.168.1.55", "192.168.1.55", 32)] // bare IP => /32
    public void Parse_ValidCidr(string text, string expectedNetwork, int expectedPrefix)
    {
        var cidr = Cidr.Parse(text);
        Assert.Equal(expectedPrefix, cidr.PrefixLength);
        Assert.Equal(expectedNetwork, cidr.NetworkAddress.ToString());
    }

    [Theory]
    [InlineData("not-an-ip/12")]
    [InlineData("172.16.0.0/33")]
    [InlineData("")]
    [InlineData("::1/64")] // IPv6 not supported
    public void TryParse_Invalid_ReturnsFalse(string text)
        => Assert.False(Cidr.TryParse(text, out _));

    [Fact]
    public void Contains_RespectsPrefix()
    {
        var net = Cidr.Parse("172.16.0.0/12");
        Assert.True(net.Contains(IPAddress.Parse("172.16.4.10")));
        Assert.True(net.Contains(IPAddress.Parse("172.31.255.255")));
        Assert.False(net.Contains(IPAddress.Parse("172.32.0.0")));
        Assert.False(net.Contains(IPAddress.Parse("10.40.0.10")));
    }

    [Fact]
    public void Overlaps_DetectsContainmentBothDirections()
    {
        var big = Cidr.Parse("172.16.0.0/12");
        var small = Cidr.Parse("172.16.4.0/24");
        Assert.True(big.Overlaps(small));
        Assert.True(small.Overlaps(big));

        var unrelated = Cidr.Parse("10.0.0.0/8");
        Assert.False(big.Overlaps(unrelated));
    }

    [Fact]
    public void FromMask_ComputesPrefix()
    {
        var cidr = Cidr.FromMask(IPAddress.Parse("172.16.0.0"), IPAddress.Parse("255.240.0.0"));
        Assert.Equal(12, cidr.PrefixLength);
        Assert.Equal("172.16.0.0/12", cidr.ToString());
    }

    [Fact]
    public void FindOverlaps_ReturnsEveryConflictingPair()
    {
        var tunneled = new[] { Cidr.Parse("172.16.0.0/12"), Cidr.Parse("172.16.4.10/32") };
        var company = new[] { Cidr.Parse("172.16.4.0/24"), Cidr.Parse("10.40.0.0/16") };

        var overlaps = CidrTools.FindOverlaps(tunneled, company);

        // 172.16/12 vs 172.16.4.0/24, and 172.16.4.10/32 vs 172.16.4.0/24 -> 2 pairs.
        Assert.Equal(2, overlaps.Count);
        Assert.All(overlaps, o => Assert.Equal("172.16.4.0/24", o.Company.ToString()));
    }
}
