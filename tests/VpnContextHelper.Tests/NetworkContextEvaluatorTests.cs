using System.Net;
using VpnContextHelper.Core.Config;
using VpnContextHelper.Core.Context;
using VpnContextHelper.Core.Net;
using VpnContextHelper.Core.Probes;
using Xunit;

namespace VpnContextHelper.Tests;

public class NetworkContextEvaluatorTests
{
    private static ProbeResult Ok(string name) => new(name, $"{name}:443", ProbeKind.Tcp, true, 5, "connected");
    private static ProbeResult Fail(string name) => new(name, $"{name}:443", ProbeKind.Tcp, false, 2000, "timeout");

    private static AppConfig Config() => new()
    {
        CompanyAccess = new AccessGroup { Label = "Company", InternalCidrs = { "172.16.4.0/24" } },
        ClientAccess = new AccessGroup { Label = "Client", VpnProfileName = "Client VPN" },
    };

    [Theory]
    [InlineData(new[] { true, true }, AccessVerdict.Reachable)]
    [InlineData(new[] { true, false }, AccessVerdict.Partial)]
    [InlineData(new[] { false, false }, AccessVerdict.Unreachable)]
    public void EvaluateGroup_VerdictReflectsProbes(bool[] oks, AccessVerdict expected)
    {
        var probes = oks.Select((ok, i) => ok ? Ok($"t{i}") : Fail($"t{i}")).ToList();
        var status = NetworkContextEvaluator.EvaluateGroup("G", probes);
        Assert.Equal(expected, status.Verdict);
    }

    [Fact]
    public void EmptyProbes_AreUnknown()
        => Assert.Equal(AccessVerdict.Unknown, NetworkContextEvaluator.EvaluateGroup("G", Array.Empty<ProbeResult>()).Verdict);

    [Fact]
    public void CompanyUnreachableWhileVpnUp_SuggestsZtna()
    {
        var vpn = new VpnState(true, IPAddress.Parse("10.20.0.2"), false, new[] { Cidr.Parse("172.16.0.0/12") });
        var ctx = NetworkContextEvaluator.Build(
            Config(), trusted: false, vpn,
            companyProbes: new[] { Fail("Bitbucket") },
            clientProbes: new[] { Ok("ClientApp") });

        Assert.Equal(AccessVerdict.Unreachable, ctx.Company.Verdict);
        Assert.Equal(AccessVerdict.Reachable, ctx.Client.Verdict);
        Assert.Contains(ctx.Messages, m => m.Contains("ZTNA", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClientUnreachable_SuggestsConnectingClientVpnByName()
    {
        var ctx = NetworkContextEvaluator.Build(
            Config(), trusted: true, VpnState.Disconnected,
            companyProbes: new[] { Ok("Bitbucket") },
            clientProbes: new[] { Fail("ClientApp") });

        Assert.Contains(ctx.Messages, m => m.Contains("Client VPN"));
    }

    [Fact]
    public void DetectsRouteOverlap_AndFlagsIt()
    {
        // Tunnel carries 172.16/12; company internal range 172.16.4.0/24 sits inside it.
        var vpn = new VpnState(true, IPAddress.Parse("10.20.0.2"), false, new[] { Cidr.Parse("172.16.0.0/12") });
        var ctx = NetworkContextEvaluator.Build(
            Config(), trusted: false, vpn,
            companyProbes: Array.Empty<ProbeResult>(),
            clientProbes: Array.Empty<ProbeResult>());

        Assert.Single(ctx.RouteOverlaps);
        Assert.Contains(ctx.Messages, m => m.Contains("overlap", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FullTunnel_DoesNotRaiseFalseOverlap()
    {
        // A full tunnel carries 0.0.0.0/0 — that must NOT be reported as "capturing" company ranges.
        var vpn = new VpnState(true, IPAddress.Parse("10.20.0.2"), true, new[] { Cidr.Parse("0.0.0.0/0") });
        var ctx = NetworkContextEvaluator.Build(
            Config(), trusted: false, vpn,
            companyProbes: Array.Empty<ProbeResult>(),
            clientProbes: Array.Empty<ProbeResult>());

        Assert.Empty(ctx.RouteOverlaps);
        Assert.DoesNotContain(ctx.Messages, m => m.Contains("overlap", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExpectViaLabel_FlowsThroughProbeResult()
    {
        var p = Ok("Bitbucket") with { ExpectVia = "ztna" };
        Assert.Equal("ztna", p.ExpectVia);
    }

    [Fact]
    public void BothReachable_SaysNoSwitchingNeeded()
    {
        var vpn = new VpnState(true, IPAddress.Parse("10.20.0.2"), false, new[] { Cidr.Parse("172.16.0.0/12") });
        var ctx = NetworkContextEvaluator.Build(
            Config(), trusted: false, vpn,
            companyProbes: new[] { Ok("Bitbucket") },
            clientProbes: new[] { Ok("ClientApp") });

        Assert.Contains(ctx.Messages, m => m.Contains("no switching needed", StringComparison.OrdinalIgnoreCase));
    }
}
