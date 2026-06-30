using System.Net;
using VpnContextHelper.Core.Net;
using Xunit;

namespace VpnContextHelper.Tests;

public class VpnStateAnalyzerTests
{
    private static readonly IPAddress[] FortiIp = { IPAddress.Parse("10.20.0.2") };

    [Fact]
    public void SplitTunnel_DetectedWithTunneledRanges()
    {
        var routes = RouteParser.ParseRoutePrint(TestFixtures.RoutePrintSplitTunnel);
        var state = VpnStateAnalyzer.Analyze(routes, FortiIp);

        Assert.True(state.Connected);
        Assert.False(state.IsFullTunnel);
        Assert.True(state.IsSplitTunnel);
        Assert.Equal("10.20.0.2", state.TunnelIp!.ToString());

        // self /32, multicast and broadcast are excluded; only real destinations remain.
        Assert.Equal(3, state.TunneledCidrs.Count);
        Assert.Contains(Cidr.Parse("172.16.0.0/12"), state.TunneledCidrs);
        Assert.Contains(Cidr.Parse("172.16.4.11/32"), state.TunneledCidrs);
        Assert.Contains(Cidr.Parse("172.16.4.10/32"), state.TunneledCidrs);
        Assert.DoesNotContain(Cidr.Parse("224.0.0.0/4"), state.TunneledCidrs);
    }

    [Fact]
    public void FullTunnel_DetectedWhenDefaultRouteOnTunnel()
    {
        var routes = RouteParser.ParseRoutePrint(TestFixtures.RoutePrintFullTunnel);
        var state = VpnStateAnalyzer.Analyze(routes, FortiIp);

        Assert.True(state.Connected);
        Assert.True(state.IsFullTunnel);
        Assert.False(state.IsSplitTunnel);
    }

    [Fact]
    public void NoFortinetIp_ReportsDisconnected()
    {
        var routes = RouteParser.ParseRoutePrint(TestFixtures.RoutePrintSplitTunnel);
        var state = VpnStateAnalyzer.Analyze(routes, Array.Empty<IPAddress>());
        Assert.False(state.Connected);
    }

    [Fact]
    public void UnrelatedFortinetIp_ReportsDisconnected()
    {
        var routes = RouteParser.ParseRoutePrint(TestFixtures.RoutePrintSplitTunnel);
        var state = VpnStateAnalyzer.Analyze(routes, new[] { IPAddress.Parse("10.99.99.99") });
        Assert.False(state.Connected);
    }
}
