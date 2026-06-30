# VPN Context Helper

A **safe, read-only** Windows helper for working across two FortiClient VPN contexts at once —
your **employer ("company")** and an external **client engagement ("client")** — without constantly
switching.

> In your deployment, fill the real names, hostnames and subnets into `config/config.json`
> (git-ignored). Everything committed here uses neutral **company/client** placeholders so the repo
> is safe to share.

> **Headline finding (see [`docs/feasibility.md`](docs/feasibility.md)):** on a fully Zero-Trust /
> ZTNA-enabled laptop you can *already* use both at the same time. Company resources are reachable via
> **ZTNA** (the Zero Trust Fabric Agent) **while the client VPN tunnel is connected** — verified live.
> Two full FortiClient VPN tunnels cannot both connect at once, but you don't need that. This tool
> tells you which resources work concurrently and helps you ask IT to close the remaining gap.

## What problem it solves

* Shows, at a glance, your **live network context**: trusted/on-net, the active FortiClient tunnel
  (split vs full, which subnets it carries), and whether your configured **company** and **client**
  resources are reachable **right now** (with the *expected* access path shown as a config label).
* Tells you plainly whether you actually need to switch VPN — and when you don't.
* Generates a **redacted diagnostic report** and a **pre-filled IT request** to extend ZTNA coverage
  (the supported fix that removes switching entirely).
* Opens FortiClient for you (UI only).

## What it does NOT do (by design)

* ❌ Does **not** connect/disconnect VPNs, or enter credentials, MFA, or SAML — you do that in
  FortiClient.
* ❌ Does **not** add, delete, or modify routes, DNS, firewall rules, services, drivers, or VPN
  profiles. It never runs `route add`.
* ❌ Does **not** bypass, disable, weaken, patch, or reverse-engineer FortiClient, Zero Trust,
  endpoint protection, certificates, or any company/client control.
* ❌ Does **not** read, store, log, or transmit secrets, cookies, tokens, SAML assertions,
  certificates, or VPN private material. Reachability checks are TCP-connect/DNS only (no payload).
* ❌ Does **not** require administrator rights (manifest is `asInvoker`).
* ❌ Does **not** hide traffic or evade monitoring.

The **only** things it writes are: the report/IT-request files you explicitly ask it to save, and an
optional local log — both passed through a secret redactor first.

## Components

| Project | What it is |
|---|---|
| `src/VpnContextHelper.Core` | Pure logic: route/ipconfig parsers, VPN-state + overlap analysis, reachability, redactor, report/IT-request builders. No I/O in the parsers — fully unit-tested. |
| `src/VpnContextHelper.Cli` (`vpnhelper`) | Headless command-line tool: `status`, `report`, `it-request`. |
| `src/VpnContextHelper.App` (`VpnContextHelper`) | WPF system-tray app with a status window and buttons. |
| `tests/VpnContextHelper.Tests` | 54 xUnit tests (route parsing, adapter detection, config, redaction, analysis). |

## Requirements

* **.NET SDK 8.0 or newer.** Projects target `net8.0` / `net8.0-windows`; with `<RollForward>Major</RollForward>`
  they build and run on a newer installed runtime (this machine has the .NET 10 SDK + runtime, no
  separate net8 runtime needed).
* **Windows 10/11** (the tray app and the live inspector use Windows networking APIs).
* **`dotnet` CLI is sufficient — Visual Studio is NOT required.** Any editor works; the build, test,
  and run commands below are pure CLI. (The solution is `.slnx`, which the .NET 10 SDK / recent VS /
  VS Code all open, but you never need to open it.)

## Setup

```powershell
cd C:\path\to\2VPNAPP

# 1) Create your config from the placeholder sample, then edit hostnames/subnets:
Copy-Item config\config.sample.json config\config.json   # config\config.json is git-ignored

# 2) Build everything
dotnet build VpnContextHelper.slnx -c Release

# 3) Run the tests
dotnet test tests\VpnContextHelper.Tests\VpnContextHelper.Tests.csproj -c Release
```

### Run the CLI

```powershell
# status (read-only): trusted/on-net, tunnel state, reachability, guidance
dotnet src\VpnContextHelper.Cli\bin\Release\net8.0\vpnhelper.dll status

# redacted diagnostic report for IT (Markdown)
dotnet src\VpnContextHelper.Cli\bin\Release\net8.0\vpnhelper.dll report report.md

# pre-filled ZTNA request for IT (Markdown)
dotnet src\VpnContextHelper.Cli\bin\Release\net8.0\vpnhelper.dll it-request it-request.md

# add --config <path> to any command to point at a specific config file
```

Example output (placeholder values; run it yourself for real data):

```
Trusted network : no
VPN tunnel      : SPLIT @ 10.20.0.2
Tunneled ranges : 172.16.0.0/12, 172.16.4.10/32, 172.16.4.11/32

Company         : Reachable
    [OK ] Bitbucket (bitbucket.company.example:443) 32ms — connected [expected: ztna]
    [OK ] Jira (jira.company.example:443) 85ms — connected [expected: ztna]
Client          : Reachable
    [OK ] Client DNS (172.16.4.10:53) 58ms — connected [expected: vpn]

Notes:
  • FortiClient tunnel active (SPLIT tunnel, 3 routed range(s)).
  • Guidance: both company and client resources are reachable right now — no switching needed.
```

> `[expected: ztna]` is a **label from your config**, not a measurement. The tool checks TCP/DNS
> reachability; it does not (and cannot, from a bare connect) prove the path taken was ZTNA vs the
> VPN tunnel vs the local LAN. It does confirm an address is outside the client tunnel's routed
> ranges, which is consistent with ZTNA/cloud delivery.

### Run the tray app

```powershell
dotnet src\VpnContextHelper.App\bin\Release\net8.0-windows\VpnContextHelper.dll
```

A shield icon appears in the system tray. Double-click for the status window; right-click for
*Refresh*, *Open FortiClient*, *Generate diagnostic report…*, *Generate IT request…*, *Exit*.
Closing the window hides it (the tray keeps it running); quit via tray → **Exit**.

## Configuration

All hostnames/subnets live in `config/config.json` (**git-ignored** — keep your real values here).
`config/config.sample.json` is the placeholder-only template. Key fields:

* `trustedNetwork.dnsSuffixes` / `beacons` — how on-net is detected.
* `companyAccess.targets` — company resources to check. `"expectVia"` ("ztna"/"vpn"/"lan"/"internet")
  is a documentation label shown next to the result; the tool reports raw reachability, not the
  measured path.
* `clientAccess.targets` + `vpnProfileName` — client resources and the FortiClient profile name.
* `companyAccess.internalCidrs` — used to flag tunnel/route overlaps.
* `redaction` — mask MAC addresses / username; add extra regex patterns.

## Required permissions

* **Standard user.** No elevation. Reading adapters, routes (`route print`), DNS, and doing TCP
  connects all work without admin.
* Writes only to the files you choose to save and the optional log directory.

## Known limitations

* Detects the **single** active FortiClient tunnel from the routing table; it cannot show a tunnel
  that FortiClient is not currently carrying. (Two full FortiClient tunnels cannot coexist in an
  SSL-VPN + cross-org IPsec setup; FortiClient 7.4.4+ *does* support concurrent IPsec IKEv2 via
  Multi-Connect, but not for that combination — see [`docs/feasibility.md`](docs/feasibility.md) §6.)
* "Trusted network" detection is a heuristic (DNS suffix or a configurable office beacon); tune it
  in config.
* Reachability is **TCP-connect / DNS** — a green result means the port is reachable, not that the
  application-level login works, and not which path the traffic took.
* ZTNA coverage is decided server-side by IT; the tool can only observe what is reachable and help
  you request more (Option B).
* The tray app has no installer; run it from the build output or add a Startup shortcut (below).

## Uninstall / rollback

There is nothing to roll back in the system — **the tool makes no system changes.** To remove it:

```powershell
# stop the tray app via tray -> Exit, then (from the repo root):
Remove-Item -Recurse -Force .\src\*\bin, .\src\*\obj
Remove-Item -Recurse -Force .\tests\*\bin, .\tests\*\obj
# if you created a Startup shortcut:
Remove-Item "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup\VpnContextHelper.lnk" -ErrorAction SilentlyContinue
# delete any reports/logs you generated, and config\config.json if it holds internal hostnames
```

To **auto-start** the tray app at login (optional), create a shortcut to the built
`VpnContextHelper.dll` (run via `dotnet`) in the Startup folder above.

## Documentation

* [`docs/quickstart.md`](docs/quickstart.md) — what to run at home / in the office / on the client VPN.
* [`docs/feasibility.md`](docs/feasibility.md) — Phase 0 evidence and conclusions.
* [`docs/architecture.md`](docs/architecture.md) — options A–D evaluated; what was built and why.
* [`docs/vm-isolation-workflow.md`](docs/vm-isolation-workflow.md) — Option C, the VM fallback.
* [`docs/it-request-template.md`](docs/it-request-template.md) — Option B request to IT.

## Safety statement

This project was built to a strict brief: **do not bypass or weaken any security control, handle no
secrets, make no destructive changes, assume no admin rights.** The design honours that — the helper
is read-only and the recommended fix (ZTNA) is *more* secure than a broad VPN, not less.
