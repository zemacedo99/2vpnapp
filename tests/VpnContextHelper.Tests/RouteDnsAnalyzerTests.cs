using VpnContextHelper.Core.Context;
using VpnContextHelper.Core.Net;
using Xunit;

namespace VpnContextHelper.Tests;

public class RouteDnsAnalyzerTests
{
    [Fact]
    public void FindSubnetConflicts_DetectsOverlap()
    {
        var company = new[] { Cidr.Parse("10.40.0.0/16") };
        var client = new[] { Cidr.Parse("10.40.5.0/24"), Cidr.Parse("172.16.0.0/12") };
        var conflicts = RouteDnsAnalyzer.FindSubnetConflicts(company, client);
        Assert.Single(conflicts);
        Assert.Equal("10.40.0.0/16", conflicts[0].A.ToString());
        Assert.Equal("10.40.5.0/24", conflicts[0].B.ToString());
    }

    [Fact]
    public void FindSubnetConflicts_ExcludesDefaultRoute()
    {
        var company = new[] { Cidr.Parse("10.40.0.0/16") };
        var client = new[] { Cidr.Parse("0.0.0.0/0") }; // full tunnel must not count as a conflict
        Assert.Empty(RouteDnsAnalyzer.FindSubnetConflicts(company, client));
    }

    [Fact]
    public void FindSubnetConflicts_NoOverlap_None()
    {
        var company = new[] { Cidr.Parse("10.40.0.0/16") };
        var client = new[] { Cidr.Parse("172.16.0.0/12") };
        Assert.Empty(RouteDnsAnalyzer.FindSubnetConflicts(company, client));
    }

    [Fact]
    public void FindDnsSuffixConflicts_SameSuffixDifferentServers_IsConflict()
    {
        var a = new DnsAssignment("Company", new[] { "corp.example" }, new[] { "10.40.0.10" });
        var b = new DnsAssignment("Client", new[] { "corp.example" }, new[] { "172.16.4.10" });
        var conflicts = RouteDnsAnalyzer.FindDnsSuffixConflicts(new[] { a, b });
        var c = Assert.Single(conflicts);
        Assert.Equal("corp.example", c.Suffix);
    }

    [Fact]
    public void FindDnsSuffixConflicts_SameSuffixSameServers_NoConflict()
    {
        var a = new DnsAssignment("Company", new[] { "corp.example" }, new[] { "10.40.0.10" });
        var b = new DnsAssignment("Client", new[] { "corp.example" }, new[] { "10.40.0.10" });
        Assert.Empty(RouteDnsAnalyzer.FindDnsSuffixConflicts(new[] { a, b }));
    }

    [Fact]
    public void FindDnsSuffixConflicts_DifferentSuffixes_NoConflict()
    {
        var a = new DnsAssignment("Company", new[] { "corp.example" }, new[] { "10.40.0.10" });
        var b = new DnsAssignment("Client", new[] { "client.example" }, new[] { "172.16.4.10" });
        Assert.Empty(RouteDnsAnalyzer.FindDnsSuffixConflicts(new[] { a, b }));
    }
}
