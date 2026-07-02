using VpnContextHelper.Core;
using VpnContextHelper.Core.Config;
using VpnContextHelper.Core.Context;
using VpnContextHelper.Core.Diagnostics;
using VpnContextHelper.Core.Forti;
using VpnContextHelper.Core.Net;
using VpnContextHelper.Core.Probes;

// vpnhelper — read-only CLI for the VPN Context Helper.
// Commands: status (default) | check | connect-both | disconnect | dry-run-routes
//           | diagnose-resource [--name <name>] [--host <host>] [--port <port>] [--category <cat>]
//           | report [out.md] | it-request [out.md] | version | help
// Options:  --config <path>
// NOTE: connect-both / disconnect are GUIDED only — this tool never establishes/tears down a
//       tunnel, never handles credentials/MFA, and never mutates routes or DNS.

string command = "status";
string? positional = null;
string? configPath = null;

for (int i = 0; i < args.Length; i++)
{
    var a = args[i];
    switch (a)
    {
        case "--config" when i + 1 < args.Length:
            configPath = args[++i];
            break;
        case "-h" or "--help" or "help":
            command = "help";
            break;
        case "--version" or "-v":
            command = "version";
            break;
        default:
            if (i == 0 && !a.StartsWith('-')) command = a;
            else if (!a.StartsWith('-')) positional = a;
            break;
    }
}

if (command == "help")
{
    PrintHelp();
    return 0;
}

if (command == "version")
{
    Console.WriteLine($"vpnhelper {typeof(Program).Assembly.GetName().Version}");
    return 0;
}

var (config, configSource) = LoadConfig(configPath);
var redactor = new Redactor(
    config.Redaction,
    config.Redaction.MaskUsernames ? new[] { Environment.UserName } : null);
var service = new ContextService(config);

Console.WriteLine($"VPN Context Helper — config: {configSource}");
Console.WriteLine("Read-only: this tool never connects/disconnects VPNs, changes routes, or touches credentials.");
Console.WriteLine();

switch (command)
{
    case "status":
    case "check":
    {
        var result = await service.EvaluateAsync(includeRawCaptures: false);
        PrintStatus(result.Context);
        return 0;
    }
    case "report":
    {
        var result = await service.EvaluateAsync(includeRawCaptures: true);
        var md = DiagnosticReportBuilder.Build(config, result.Data, result.Context, redactor);
        var outPath = positional ?? "vpn-diagnostic-report.md";
        File.WriteAllText(outPath, md);
        Console.WriteLine($"Diagnostic report written to: {Path.GetFullPath(outPath)}");
        Console.WriteLine("Secrets are redacted; review before sending to IT.");
        return 0;
    }
    case "it-request":
    {
        var result = await service.EvaluateAsync(includeRawCaptures: false);
        var md = ItRequestBuilder.Build(config, result.Data, result.Context, redactor);
        var outPath = positional ?? "it-request.md";
        File.WriteAllText(outPath, md);
        Console.WriteLine($"IT request written to: {Path.GetFullPath(outPath)}");
        return 0;
    }
    case "connect-both":
    {
        var profiles = new FortiClientProfileInspector().GetProfiles();
        var (company, client) = ResolveProfilePair(profiles, config);
        var decision = DualConnectFeasibility.Evaluate(company, client);
        PrintDualConnect(profiles, company, client, decision, config);
        // exit 3 signals "not possible on this host"; 0 = supported (still connect manually in FortiClient)
        return decision.HostOnlyDualPossible ? 0 : 3;
    }
    case "disconnect":
    {
        Console.WriteLine("Disconnect is NOT automated.");
        Console.WriteLine("A FortiClient connect/disconnect CLI (FortiVPN.exe --cli) exists only in 7.4.6+; it is not");
        Console.WriteLine("documented for this 7.4.5 build, and this app deliberately does not drive it (it handles credentials).");
        Console.WriteLine("Use the FortiClient UI (tray > the profile > Disconnect). This tool never handles credentials/MFA.");
        return 0;
    }
    case "dry-run-routes":
    {
        var result = await service.EvaluateAsync(includeRawCaptures: false);
        DryRunRoutes(result.Context, config);
        return 0;
    }
    case "diagnose-resource":
    {
        // Resolve target: --name matches a config target by name, --host overrides or provides the hostname.
        string? targetName = FindFlag(args, "--name") ?? FindFlag(args, "-n");
        string? targetHost = FindFlag(args, "--host") ?? FindFlag(args, "-H") ?? positional;
        string? portStr   = FindFlag(args, "--port") ?? FindFlag(args, "-p");
        string category   = FindFlag(args, "--category") ?? "ztna";
        int port = int.TryParse(portStr, out var p) ? p : 443;

        // Try to find the target in config by name
        Target? configTarget = null;
        if (targetName is not null)
        {
            configTarget = config.CompanyAccess.Targets
                .Concat(config.ClientAccess.Targets)
                .FirstOrDefault(t => string.Equals(t.Name, targetName, StringComparison.OrdinalIgnoreCase));

            if (configTarget is null)
            {
                Console.Error.WriteLine($"No target named '{targetName}' in config. " +
                    "Check companyAccess/clientAccess targets in config/config.json.");
                Console.Error.WriteLine("Available target names:");
                foreach (var t in config.CompanyAccess.Targets.Concat(config.ClientAccess.Targets))
                    Console.Error.WriteLine($"  {t.Name}");
                return 2;
            }
        }

        string host = configTarget?.Host ?? targetHost ?? string.Empty;
        if (string.IsNullOrWhiteSpace(host))
        {
            Console.Error.WriteLine("diagnose-resource requires --name <config-target-name> or --host <hostname>.");
            Console.Error.WriteLine("Example: vpnhelper diagnose-resource --name company-bitbucket");
            Console.Error.WriteLine("         vpnhelper diagnose-resource --host resource.company.example --port 443");
            return 2;
        }

        string label    = configTarget?.Name ?? targetName ?? host;
        int    resolvedPort = configTarget?.Port ?? port;
        string resolvedCat  = configTarget?.ExpectVia ?? category;

        var runner = new ResourceDiagnosticRunner(timeoutMs: config.ProbeTimeoutMs);
        var diagnosis = await runner.DiagnoseAsync(label, host, resolvedPort, resolvedCat).ConfigureAwait(false);
        PrintResourceDiagnosis(diagnosis, config);
        return diagnosis.Classify() == ResourceDiagnosisClassification.Reachable ? 0 : 1;
    }
    default:
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintHelp();
        return 2;
}

static void PrintStatus(NetworkContext ctx)
{
    Console.WriteLine($"Trusted network : {(ctx.TrustedNetworkDetected ? "yes" : "no")}");
    Console.WriteLine($"VPN tunnel      : {(ctx.Vpn.Connected ? $"{(ctx.Vpn.IsFullTunnel ? "FULL" : "SPLIT")} @ {ctx.Vpn.TunnelIp}" : "none")}");
    if (ctx.Vpn.TunneledCidrs.Count > 0)
        Console.WriteLine($"Tunneled ranges : {string.Join(", ", ctx.Vpn.TunneledCidrs)}");
    Console.WriteLine();
    PrintGroup(ctx.Company);
    PrintGroup(ctx.Client);
    Console.WriteLine();
    Console.WriteLine("Notes:");
    foreach (var m in ctx.Messages) Console.WriteLine($"  • {m}");
}

static void PrintGroup(GroupStatus g)
{
    Console.WriteLine($"{g.Label,-16}: {g.Verdict}");
    foreach (var p in g.Probes)
    {
        var expect = p.ExpectVia is null ? "" : $" [expected: {p.ExpectVia}]";
        Console.WriteLine($"    [{(p.Ok ? "OK " : "ERR")}] {p.Name} ({p.Target}) {p.LatencyMs}ms — {p.Detail}{expect}");
    }
}

static (FortiVpnProfile company, FortiVpnProfile client) ResolveProfilePair(
    IReadOnlyList<FortiVpnProfile> profiles, AppConfig config)
{
    FortiVpnProfile? Find(string? name) =>
        name is null ? null : profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    VpnProtocol Inferred() =>
        profiles.Count > 0 && profiles.All(p => p.Protocol == VpnProtocol.SslVpn) ? VpnProtocol.SslVpn : VpnProtocol.Unknown;

    var client = Find(config.ClientAccess.VpnProfileName);
    var company = Find(config.CompanyAccess.VpnProfileName)
                  ?? profiles.FirstOrDefault(p => client is null || !string.Equals(p.Name, client.Name, StringComparison.OrdinalIgnoreCase));

    client ??= new FortiVpnProfile(config.ClientAccess.VpnProfileName ?? "(client)", Inferred());
    company ??= new FortiVpnProfile(config.CompanyAccess.VpnProfileName ?? "(company)", Inferred());
    return (company, client);
}

static void PrintDualConnect(
    IReadOnlyList<FortiVpnProfile> profiles, FortiVpnProfile company, FortiVpnProfile client,
    DualConnectDecision d, AppConfig config)
{
    int ssl = profiles.Count(p => p.Protocol == VpnProtocol.SslVpn);
    int ipsec = profiles.Count(p => p.Protocol == VpnProtocol.IpsecVpn);
    Console.WriteLine("Host-only dual-VPN feasibility");
    Console.WriteLine($"  Discovered profiles : {profiles.Count} (SSL-VPN: {ssl}, IPsec: {ipsec})");
    Console.WriteLine($"  Company profile     : {company.Name} [{company.Protocol}]");
    Console.WriteLine($"  Client profile      : {client.Name} [{client.Protocol}]");
    Console.WriteLine($"  Verdict             : {d.Verdict}");
    Console.WriteLine($"  Host-only dual VPN  : {(d.HostOnlyDualPossible ? "POSSIBLE" : "NOT POSSIBLE")}");
    Console.WriteLine("  Why:");
    foreach (var r in d.Reasons) Console.WriteLine($"    - {r}");
    if (d.WhatWouldNeedToChange.Count > 0)
    {
        Console.WriteLine("  What would need to change:");
        foreach (var r in d.WhatWouldNeedToChange) Console.WriteLine($"    - {r}");
    }
    if (d.WhoMustChange.Count > 0)
        Console.WriteLine($"  Who must change     : {string.Join("; ", d.WhoMustChange)}");

    if (d.HostOnlyDualPossible)
    {
        Console.WriteLine();
        Console.WriteLine("  This setup supports concurrent tunnels, but connect them yourself in the FortiClient UI.");
        Console.WriteLine("  (A 7.4.6+ connect CLI exists, but this app does not drive it — it would handle credentials.)");
    }
    else
    {
        Console.WriteLine();
        Console.WriteLine("  Supported host-only path for concurrent ACCESS (no dual tunnels):");
        Console.WriteLine($"    • Keep the client VPN connected, and reach {config.CompanyAccess.Label} resources via ZTNA");
        Console.WriteLine("      (the Zero Trust Fabric Agent runs concurrently with a VPN).");
        Console.WriteLine("    • Run 'vpnhelper status' to see what is reachable right now.");
        Console.WriteLine("    • If a needed resource is not reachable, run 'vpnhelper it-request' to ask IT to");
        Console.WriteLine("      publish it as a ZTNA application. See docs/host-only-dual-vpn-feasibility.md.");
    }
}

static void DryRunRoutes(NetworkContext ctx, AppConfig config)
{
    Console.WriteLine("DRY-RUN — this tool will NOT change any route or DNS setting.");
    Console.WriteLine();
    Console.WriteLine($"Active tunnel : {(ctx.Vpn.Connected ? $"{(ctx.Vpn.IsFullTunnel ? "FULL" : "SPLIT")} @ {ctx.Vpn.TunnelIp}" : "none")}");
    if (ctx.Vpn.TunneledCidrs.Count > 0)
        Console.WriteLine($"Tunneled      : {string.Join(", ", ctx.Vpn.TunneledCidrs)}");
    Console.WriteLine();

    var companyCidrs = config.CompanyAccess.InternalCidrs
        .Select(s => Cidr.TryParse(s, out var c) ? (Cidr?)c : null)
        .Where(c => c.HasValue).Select(c => c!.Value);
    var conflicts = RouteDnsAnalyzer.FindSubnetConflicts(companyCidrs, ctx.Vpn.TunneledCidrs);
    if (conflicts.Count == 0)
        Console.WriteLine("Subnet conflicts : none between configured company ranges and the active tunnel.");
    else
    {
        Console.WriteLine("Subnet conflicts (a company range is captured by the active tunnel):");
        foreach (var c in conflicts) Console.WriteLine($"    - {c.A} overlaps {c.B}");
    }

    Console.WriteLine();
    Console.WriteLine("Planned route mutations : 0");
    Console.WriteLine("Reason: host-only dual VPN is not supported in this setup, so there is no second tunnel to");
    Console.WriteLine("split-route toward. This tool never adds routes to reach a network you are not tunnelled");
    Console.WriteLine("into, and never bridges the two contexts. Nothing was applied; no rollback needed.");
}

static string? FindFlag(string[] args, string flag)
{
    for (int i = 0; i < args.Length - 1; i++)
        if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    return null;
}

static void PrintResourceDiagnosis(ResourceDiagnosis d, AppConfig config)
{
    Console.WriteLine("diagnose-resource — read-only ZTNA reachability check");
    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine($"  Resource : {d.Name}");
    Console.WriteLine($"  Host     : {d.Host}");
    Console.WriteLine($"  Port     : {d.Port}");
    Console.WriteLine($"  Category : {d.Category}");
    Console.WriteLine();

    Console.WriteLine("FortiClient processes");
    PrintProcess("FortiProxy (ZTNA proxy)  ", d.AgentState.ProxyRunning);
    PrintProcess("FortiTcs (ZT compliance) ", d.AgentState.TelemetryRunning);
    PrintProcess("FortiESNAC (EMS mgmt)    ", d.AgentState.EmsRunning);
    PrintProcess("FortiSSLVPNdaemon        ", d.AgentState.SslVpnDaemonRunning);
    PrintProcess("FortiVPN                 ", d.AgentState.VpnRunning);
    PrintProcess("FortiTray                ", d.AgentState.TrayRunning);
    Console.WriteLine();

    Console.WriteLine("ZTNA proxy listeners (expected: 999, 17713, 17714, 17715)");
    foreach (var port in ZtnaAgentState.ExpectedZtnaListenerPorts)
    {
        var present = d.AgentState.ActiveListenerPorts.Contains(port);
        Console.WriteLine($"  Port {port,-5} : {(present ? "present" : "ABSENT")}");
    }
    Console.WriteLine();

    Console.WriteLine("DNS servers (current — informational; client VPN may override all interfaces)");
    if (d.AgentState.DnsServersByInterface.Count == 0)
        Console.WriteLine("  (none detected)");
    foreach (var (iface, servers) in d.AgentState.DnsServersByInterface)
        Console.WriteLine($"  {iface,-38} {string.Join(", ", servers)}");
    if (d.AgentState.ClientVpnDnsOverridesAllInterfaces())
        Console.WriteLine("  NOTE: all interfaces share the same DNS server set — client VPN may be overriding DNS globally.");
    Console.WriteLine();

    Console.WriteLine("DNS check");
    Console.WriteLine($"  Result : {(d.DnsResult.Ok ? "OK" : "FAIL")} — {d.DnsResult.Detail}  ({d.DnsResult.LatencyMs}ms)");
    Console.WriteLine();

    Console.WriteLine("TCP check");
    if (d.TcpResult is null)
        Console.WriteLine("  Skipped — DNS did not resolve; no address to connect to.");
    else
        Console.WriteLine($"  Result : {(d.TcpResult.Ok ? "OK" : "FAIL")} — {d.TcpResult.Detail}  ({d.TcpResult.LatencyMs}ms)");
    Console.WriteLine();

    Console.WriteLine("Diagnosis");
    Console.WriteLine($"  Classification : {d.Classify()}");
    Console.WriteLine();
    Console.WriteLine("  Conclusion:");
    foreach (var line in WrapLines(d.HumanReadableConclusion(), 76))
        Console.WriteLine($"    {line}");
    Console.WriteLine();
    Console.WriteLine("  Next safe step:");
    foreach (var line in WrapLines(d.NextSafeAction(), 76))
        Console.WriteLine($"    {line}");
    Console.WriteLine();

    if (!d.AgentState.ProxyRunning)
    {
        Console.WriteLine("  ─────────────────────────────────────────────────────");
        Console.WriteLine("  KEY POINT: 'Zero Trust Telemetry: Connected' in the FortiClient UI");
        Console.WriteLine("  reflects the EMS management-plane (FortiESNAC), NOT the ZTNA");
        Console.WriteLine("  data-plane proxy (FortiProxy). Both must be active for ZTNA to work.");
        Console.WriteLine("  ─────────────────────────────────────────────────────");
    }
}

static void PrintProcess(string label, bool running)
    => Console.WriteLine($"  {label}: {(running ? "running" : "NOT RUNNING  ← check this")}");

static IEnumerable<string> WrapLines(string text, int width)
{
    // Simple word-wrap at the given column width
    var words = text.Split(' ');
    var line = new System.Text.StringBuilder();
    foreach (var word in words)
    {
        if (line.Length > 0 && line.Length + 1 + word.Length > width)
        {
            yield return line.ToString();
            line.Clear();
        }
        if (line.Length > 0) line.Append(' ');
        line.Append(word);
    }
    if (line.Length > 0) yield return line.ToString();
}

static (AppConfig, string) LoadConfig(string? explicitPath)
    => ConfigLocator.Find(explicitPath, msg => Console.Error.WriteLine(msg));

static void PrintHelp()
{
    Console.WriteLine("""
        vpnhelper — read-only VPN context diagnostics (no system changes)

        USAGE:
          vpnhelper [command] [out.md] [--config <path>]

        COMMANDS:
          status          Show trusted-network, VPN tunnel state and reachability (default)
          check           Alias for status
          connect-both    Analyse whether host-only dual VPN is possible; print the verdict + guidance
                          (GUIDED ONLY — never establishes a tunnel or handles credentials/MFA)
          disconnect      Explain how to disconnect (this app never drives FortiClient's 7.4.6+ connect CLI — it handles credentials)
          dry-run-routes  Read-only route/DNS conflict analysis; applies NOTHING
          diagnose-resource  Read-only ZTNA resource reachability check: FortiProxy process,
                          ZTNA listeners, DNS, TCP — in order. Prints conclusion + next safe step.
                          --name <config-target-name>   look up host/port from config
                          --host <hostname>             explicit hostname
                          --port <port>                 TCP port (default 443)
                          --category <cat>              expected path label: ztna|vpn|lan (default ztna)
          report [out]    Write a redacted Markdown diagnostic report (default: vpn-diagnostic-report.md)
          it-request[out] Write a ready-to-send IT request (default: it-request.md)
          version         Print version
          help            This help

        Config is searched at: --config path, ./config/config.json, ./config.json.
        """);
}

// Marker type so typeof(Program) works with top-level statements.
internal sealed partial class Program { }
