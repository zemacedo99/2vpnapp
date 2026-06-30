using VpnContextHelper.Core;
using VpnContextHelper.Core.Config;
using VpnContextHelper.Core.Context;
using VpnContextHelper.Core.Diagnostics;

// vpnhelper — read-only CLI for the VPN Context Helper.
// Commands: status (default) | check | report [out.md] | it-request [out.md] | version | help
// Options:  --config <path>

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

static (AppConfig, string) LoadConfig(string? explicitPath)
    => ConfigLocator.Find(explicitPath, msg => Console.Error.WriteLine(msg));

static void PrintHelp()
{
    Console.WriteLine("""
        vpnhelper — read-only VPN context diagnostics (no system changes)

        USAGE:
          vpnhelper [command] [out.md] [--config <path>]

        COMMANDS:
          status        Show trusted-network, VPN tunnel state and reachability (default)
          check         Alias for status
          report [out]  Write a redacted Markdown diagnostic report (default: vpn-diagnostic-report.md)
          it-request[out] Write a ready-to-send IT request (default: it-request.md)
          version       Print version
          help          This help

        Config is searched at: --config path, ./config/config.json, ./config.json.
        """);
}

// Marker type so typeof(Program) works with top-level statements.
internal sealed partial class Program { }
