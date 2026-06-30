using System.Net;
using VpnContextHelper.Core.Net;
using Xunit;

namespace VpnContextHelper.Tests;

public class RouteParserTests
{
    [Fact]
    public void ParsesOnlyActiveRoutes_NotPersistentOrInterfaceList()
    {
        var routes = RouteParser.ParseRoutePrint(TestFixtures.RoutePrintSplitTunnel);

        // 8 active routes; the persistent "Default" route and the Interface List are excluded.
        Assert.Equal(8, routes.Count);
        Assert.DoesNotContain(routes, r => r.Gateway == "Default");
    }

    [Fact]
    public void ParsesDefaultRoute()
    {
        var routes = RouteParser.ParseRoutePrint(TestFixtures.RoutePrintSplitTunnel);
        var def = Assert.Single(routes, r => r.IsDefaultRoute);
        Assert.Equal("192.168.1.1", def.Gateway);
        Assert.Equal("192.168.1.10", def.InterfaceIp.ToString());
        Assert.Equal(35, def.Metric);
    }

    [Fact]
    public void ParsesOnLinkAndGatewayRoutes()
    {
        var routes = RouteParser.ParseRoutePrint(TestFixtures.RoutePrintSplitTunnel);

        var mt = Assert.Single(routes, r => r.Destination.Equals(IPAddress.Parse("172.16.0.0")));
        Assert.Equal("10.20.0.1", mt.Gateway);
        Assert.False(mt.IsOnLink);
        Assert.Equal(12, mt.PrefixLength);
        Assert.Equal("172.16.0.0/12", mt.ToCidr().ToString());

        var onlink = Assert.Single(routes, r => r.Destination.Equals(IPAddress.Parse("192.168.1.0")));
        Assert.True(onlink.IsOnLink);
    }

    [Fact]
    public void EmptyInput_ReturnsEmpty()
        => Assert.Empty(RouteParser.ParseRoutePrint(""));
}
