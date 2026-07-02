using VpnContextHelper.Core.Diagnostics;
using VpnContextHelper.Core.Forti;
using VpnContextHelper.Core.Probes;
using Xunit;

namespace VpnContextHelper.Tests;

/// <summary>
/// Unit tests for the ZTNA resource diagnostic — covers the pure classification and conclusion
/// logic in <see cref="ZtnaAgentState"/> and <see cref="ResourceDiagnosis"/>.
/// No live system access; fixtures use placeholder process names and IPs.
/// </summary>
public class ZtnaResourceDiagnosticTests
{
    // ─── ZtnaAgentState ───────────────────────────────────────────────────────

    private static ZtnaAgentState AgentState(
        bool proxy = false, bool tcs = false, bool ems = true,
        int[]? ports = null,
        (string alias, string[] dns)[]? dnsPerIface = null)
    {
        var dnsServers = (dnsPerIface ?? Array.Empty<(string, string[])>())
            .Select(t => (t.alias, (IReadOnlyList<string>)t.dns.ToList()))
            .ToList<(string, IReadOnlyList<string>)>();

        return new ZtnaAgentState(
            ProxyRunning: proxy,
            TelemetryRunning: tcs,
            EmsRunning: ems,
            SslVpnDaemonRunning: true,
            VpnRunning: true,
            TrayRunning: true,
            ActiveListenerPorts: (ports ?? Array.Empty<int>()).ToList(),
            DnsServersByInterface: dnsServers);
    }

    [Fact]
    public void ZtnaActive_RequiresProxyAndListeners()
    {
        Assert.False(AgentState(proxy: false, ports: [999]).ZtnaDataPlaneActive);
        Assert.False(AgentState(proxy: true, ports: []).ZtnaDataPlaneActive);
        Assert.True(AgentState(proxy: true, ports: [999, 17713]).ZtnaDataPlaneActive);
    }

    [Fact]
    public void DiagnosisSummary_BothDown()
    {
        var s = AgentState(proxy: false, tcs: false).DiagnosisSummary();
        Assert.Contains("FortiProxy", s);
        Assert.Contains("FortiTcs", s);
        Assert.Contains("NOT running", s);
    }

    [Fact]
    public void DiagnosisSummary_ProxyDownTcsUp()
    {
        var s = AgentState(proxy: false, tcs: true).DiagnosisSummary();
        Assert.Contains("FortiProxy", s);
        Assert.Contains("NOT running", s);
        Assert.DoesNotContain("FortiTcs is NOT", s);
    }

    [Fact]
    public void DiagnosisSummary_ProxyUpNoListeners()
    {
        var s = AgentState(proxy: true, tcs: true, ports: []).DiagnosisSummary();
        Assert.Contains("no ZTNA listener", s, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiagnosisSummary_FullyOperational()
    {
        var s = AgentState(proxy: true, tcs: true, ports: [999, 17713, 17714, 17715]).DiagnosisSummary();
        Assert.Contains("operational", s, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClientVpnDnsOverride_DetectsAllSameServers()
    {
        var dns = new[] { ("eth0", new[] { "10.20.0.1", "10.20.0.2" }),
                          ("eth1", new[] { "10.20.0.1", "10.20.0.2" }) };
        Assert.True(AgentState(dnsPerIface: dns).ClientVpnDnsOverridesAllInterfaces());
    }

    [Fact]
    public void ClientVpnDnsOverride_DifferentServersNotFlagged()
    {
        var dns = new[] { ("eth0", new[] { "10.20.0.1" }),
                          ("eth1", new[] { "192.168.1.1" }) };
        Assert.False(AgentState(dnsPerIface: dns).ClientVpnDnsOverridesAllInterfaces());
    }

    // ─── ResourceDiagnosis ────────────────────────────────────────────────────

    private static ProbeResult DnsOk() =>
        new("test", "resource.company.example", ProbeKind.Dns, true, 10, "1 address(es)");

    private static ProbeResult DnsFail() =>
        new("test", "resource.company.example", ProbeKind.Dns, false, 100, "no records");

    private static ProbeResult TcpOk() =>
        new("test", "resource.company.example:443", ProbeKind.Tcp, true, 20, "connected");

    private static ProbeResult TcpFail() =>
        new("test", "resource.company.example:443", ProbeKind.Tcp, false, 2000, "timeout");

    private static ResourceDiagnosis Diag(ZtnaAgentState agent, ProbeResult dns, ProbeResult? tcp)
        => new("test", "resource.company.example", 443, "ztna", agent, dns, tcp);

    [Fact]
    public void Classify_ZtnaProxyNotRunning()
    {
        var d = Diag(AgentState(proxy: false), DnsOk(), TcpOk());
        Assert.Equal(ResourceDiagnosisClassification.ZtnaProxyNotRunning, d.Classify());
    }

    [Fact]
    public void Classify_ZtnaListenersMissing()
    {
        var d = Diag(AgentState(proxy: true, ports: []), DnsOk(), TcpOk());
        Assert.Equal(ResourceDiagnosisClassification.ZtnaListenersMissing, d.Classify());
    }

    [Fact]
    public void Classify_DnsFailure()
    {
        var d = Diag(AgentState(proxy: true, ports: [999]), DnsFail(), null);
        Assert.Equal(ResourceDiagnosisClassification.DnsFailure, d.Classify());
    }

    [Fact]
    public void Classify_TcpFailure()
    {
        var d = Diag(AgentState(proxy: true, ports: [999]), DnsOk(), TcpFail());
        Assert.Equal(ResourceDiagnosisClassification.TcpFailure, d.Classify());
    }

    [Fact]
    public void Classify_Reachable()
    {
        var d = Diag(AgentState(proxy: true, ports: [999]), DnsOk(), TcpOk());
        Assert.Equal(ResourceDiagnosisClassification.Reachable, d.Classify());
    }

    [Fact]
    public void HumanReadableConclusion_NonEmpty_ForAllCases()
    {
        var agents = new[]
        {
            AgentState(proxy: false),
            AgentState(proxy: true, ports: []),
            AgentState(proxy: true, ports: [999]),
        };
        var dnsSets = new[] { (DnsOk(), (ProbeResult?)TcpOk()), (DnsOk(), TcpFail()), (DnsFail(), null) };

        foreach (var agent in agents)
        foreach (var (dns, tcp) in dnsSets)
        {
            var conclusion = Diag(agent, dns, tcp).HumanReadableConclusion();
            Assert.False(string.IsNullOrWhiteSpace(conclusion));
        }
    }

    [Fact]
    public void NextSafeAction_NonEmpty_ForAllCases()
    {
        foreach (var cls in Enum.GetValues<ResourceDiagnosisClassification>())
        {
            // Build a diagnosis that produces this classification
            var (agent, dns, tcp) = cls switch
            {
                ResourceDiagnosisClassification.ZtnaProxyNotRunning
                    => (AgentState(proxy: false), DnsOk(), (ProbeResult?)TcpOk()),
                ResourceDiagnosisClassification.ZtnaListenersMissing
                    => (AgentState(proxy: true, ports: []), DnsOk(), TcpOk()),
                ResourceDiagnosisClassification.DnsFailure
                    => (AgentState(proxy: true, ports: [999]), DnsFail(), null),
                ResourceDiagnosisClassification.TcpFailure
                    => (AgentState(proxy: true, ports: [999]), DnsOk(), TcpFail()),
                ResourceDiagnosisClassification.Reachable
                    => (AgentState(proxy: true, ports: [999]), DnsOk(), TcpOk()),
                _   => (AgentState(proxy: true, ports: [999]), DnsOk(), null),
            };
            var action = Diag(agent, dns, tcp).NextSafeAction();
            Assert.False(string.IsNullOrWhiteSpace(action));
        }
    }

    [Fact]
    public void ZtnaProxyNotRunning_Conclusion_MentionsEmsDistinction()
    {
        var d = Diag(AgentState(proxy: false), DnsFail(), null);
        var conclusion = d.HumanReadableConclusion();
        // Must mention the EMS/management-plane vs data-plane distinction so the user knows
        // why the UI says Connected but the resource is down.
        Assert.Contains("EMS", conclusion, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FortiProxy", conclusion);
    }
}
