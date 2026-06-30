# Quickstart — what to run, when

One command answers "what do I have access to right now, and do I need to switch VPN?":

```powershell
dotnet src\VpnContextHelper.Cli\bin\Release\net8.0\vpnhelper.dll status
```

(or double-click the tray app and read the status window). It is **read-only** — it never connects,
disconnects, or changes anything. Below, "company" = your employer's resources (reached via ZTNA or
the company VPN) and "client" = the external client engagement (reached via the client VPN profile).

---

## How to read the output

```
Trusted network : no | yes (on-net)
VPN tunnel      : none | SPLIT @ <ip> | FULL @ <ip>
Tunneled ranges : <subnets the client/company tunnel carries>

Company         : Reachable | Partial | Unreachable
Client          : Reachable | Partial | Unreachable

Notes:
  • ... plain-language guidance, including "no switching needed" when both work
```

**You already have what you need when both `Company` and `Client` show `Reachable`** and the last
note says *"both company and client resources are reachable right now — no switching needed."* That
is the normal, good state — ZTNA is serving company resources while the client VPN tunnel is up.

---

## Scenario: I'm at home (remote)

1. Connect the **client VPN** in FortiClient if you need client resources.
2. Run `vpnhelper status`.
3. Expected good case: `Client = Reachable`, and `Company = Reachable` for any resource published via
   ZTNA. If a company resource shows **Unreachable**, see *"…unreachable"* below — it likely is not
   ZTNA-published yet.

## Scenario: I'm in the office (on the trusted network)

1. You usually do **not** need the company VPN here — Zero Trust treats the office network as safe, so
   company resources are reachable directly.
2. Connect the **client VPN** for client work; its split tunnel only carries the client ranges, so
   office/company access keeps working alongside it.
3. Run `vpnhelper status`. Expect `Trusted network : yes` (if your office DNS suffix / beacon is set in
   `config/config.json`) and both groups `Reachable`.
   - If `Trusted network` shows `no` in the office, add your office-only DNS suffix or a beacon
     host:port under `trustedNetwork` in `config/config.json` — it is only a display hint.

## Scenario: the client VPN is connected

1. Run `vpnhelper status`.
2. `VPN tunnel : SPLIT @ <ip>` confirms the client tunnel is up and which ranges it carries.
3. `Client = Reachable` → client work is fine.
4. `Company = Reachable` → ZTNA (or cloud) is serving company resources concurrently — **keep the
   client VPN connected, no need to switch.**
5. `Company = Partial/Unreachable` → only the listed resources are the problem; everything else is
   fine. Don't disconnect the client VPN reflexively — first check *which* resource is failing.

## How to tell if I already have access (without disconnecting anything)

`vpnhelper status` does exactly this, read-only. A `Reachable` line means a TCP/DNS connection to that
resource succeeds **in your current state**. If it is green while the client VPN is up, you have
concurrent access and do not need to switch.

> Caveat: a green result means the port answered — not which path it took (ZTNA vs VPN vs LAN) and not
> that the app-level login works. The `[expected: …]` tag is your config label, not a measurement.

## What to send to IT if a company resource is Unreachable

When a company resource you need shows **Unreachable** while the client VPN is connected, it is
probably not published via ZTNA yet. Generate the request and send it to your company IT:

```powershell
dotnet src\VpnContextHelper.Cli\bin\Release\net8.0\vpnhelper.dll it-request it-request.md
dotnet src\VpnContextHelper.Cli\bin\Release\net8.0\vpnhelper.dll report report.md
```

- `it-request.md` — a ready-to-edit request asking IT to **publish that resource as a ZTNA
  application** (the supported fix; no bypass). Fill the bracketed placeholders and the resource list.
- `report.md` — a redacted diagnostic (no secrets) you attach as evidence.

Then: keep the client VPN connected, send both files to IT, and re-run `vpnhelper status` after they
publish the resource — it should flip to `Reachable`. Full background:
[`architecture.md`](architecture.md) (Option B) and [`it-request-template.md`](it-request-template.md).

If IT says a resource genuinely cannot be ZTNA-published, use the VM fallback in
[`vm-isolation-workflow.md`](vm-isolation-workflow.md) (get company + client approval first).
