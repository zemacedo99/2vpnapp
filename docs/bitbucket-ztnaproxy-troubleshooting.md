# Troubleshooting — company Bitbucket unreachable while client VPN is connected

> All addresses, hostnames, machine/user names in this document are **illustrative placeholders**.
> Real values live only in the git-ignored `config/config.json` and `config/sensitive-tokens.local`.

---

## Quick command

```powershell
dotnet src\VpnContextHelper.Cli\bin\Release\net8.0\vpnhelper.dll diagnose-resource --name company-bitbucket
```

This command runs a read-only check of: FortiClient ZTNA processes, ZTNA listener ports, DNS
resolution, and TCP reachability — in that order. It prints a human-readable conclusion and the
one safe next step.

---

## Background: how Bitbucket access works while the client VPN is connected

In the known-good state, company Bitbucket is **not** reachable through the client VPN tunnel.
Instead it is served by the **ZTNA access proxy (FortiProxy)**, which runs on the host alongside
the client VPN tunnel:

```
You (host) ─── ZTNA proxy (FortiProxy, loopback:999) ─── ZTNA gateway ─── company Bitbucket
              └── client VPN tunnel ─────────────────────────────── client resources
```

ZTNA is **per-published-destination** (not a subnet tunnel), so both paths coexist.

### What makes it work (known-good component state)

| Component | Process | Ports / Notes |
|---|---|---|
| EMS management plane | `FortiESNAC` | manages the endpoint; its status is what the FortiClient UI "Zero Trust Telemetry: Connected" label reflects |
| ZTNA compliance | `FortiTcs` | runs posture checks |
| **ZTNA access proxy** | **`FortiProxy`** | **loopback 999, 0.0.0.0:17713-17715 — the thing that carries the traffic** |
| SSL-VPN daemon | `FortiSSLVPNdaemon` | carries the client VPN tunnel (port 8053) |
| VPN client | `FortiVPN` | |

**All of the above must be running for concurrent access to work.**

---

## Symptom pattern: DNS returns only SOA, no A record

`Resolve-DnsName bitbucket.company.example` returns only the `company.example` SOA record and no
IPv4 address. This means:

1. The name is **not publicly resolvable** (it is internal-only), AND
2. The current DNS path does not have an A record for it.

### Why the ZTNA proxy matters for DNS

The ZTNA gateway resolves company internal names on your behalf when you go through the proxy.
When `FortiProxy` is running and intercepting `bitbucket.company.example:443`, the name
resolution happens at the gateway, not locally. When `FortiProxy` is not running, the local OS
must resolve the name — but the current DNS servers (often pushed by the client VPN) don't know
the internal name.

### Why the client VPN overrides all interface DNS

A typical client VPN profile pushes its DNS servers via DHCP option 6. Windows applies these
to all active adapters, not only the VPN adapter. So with the client VPN connected:

- Every interface shows the client VPN's DNS servers
- Company internal names that require company DNS / ZTNA gateway resolution are not resolvable
- `Resolve-DnsName company.internal.host` → SOA only, no A record

This is **expected** and is not itself a bug — the ZTNA proxy is the designed solution. The
problem is when `FortiProxy` is not running.

---

## Root cause: FortiProxy / FortiTcs not running

**Important:** The FortiClient UI "Zero Trust Telemetry: Connected" label refers to the
**EMS management-plane connection (FortiESNAC)**. This is **not** the same as the ZTNA data-plane
proxy being active. It is possible to be "Connected" to EMS and have ZTNA fully inactive.

| UI label | What it reflects | Process |
|---|---|---|
| "Zero Trust Telemetry: Connected" | EMS registration + management plane | `FortiESNAC` |
| ZTNA access proxy (data plane) | Absent if FortiProxy is not running | `FortiProxy` |

### How to tell the difference

| Check | Known-good | Failing state |
|---|---|---|
| `FortiProxy` process | running | **not running** |
| `FortiTcs` process | running | not running |
| Port 999 listener | present (loopback) | absent |
| Ports 17713-17715 | present | absent |
| `Resolve-DnsName bitbucket.company.example` | returns A record | SOA only, no address |
| TCP 443 to Bitbucket | reachable | unreachable (no address / no proxy) |

Use `vpnhelper diagnose-resource --name company-bitbucket` to check all of the above in one command.

---

## Triggers for ZTNA proxy not running

The ZTNA components (FortiProxy, FortiTcs) are managed by the FortiClient scheduler and the EMS
policy. They may fail to start or stop running when:

- **Unfamiliar network**: the EMS cannot reach the ZTNA gateway on this network segment, so it
  does not apply the ZTNA policy. The endpoint is on a different subnet or gateway than usual.
- **Posture / compliance failure**: FortiTcs runs a compliance check; if it fails, the ZTNA policy
  may be withheld (posture-gated ZTNA).
- **Crash or policy refresh**: FortiProxy may have crashed or the EMS pushed a policy change that
  temporarily stopped it.
- **First connection on a new network**: the ZTNA gateway may need re-authentication on a network
  it hasn't seen before.

---

## Safe next steps (no system changes)

1. **Open FortiClient → Zero Trust Telemetry tab.**
   - Check whether it shows "Connected" AND green compliance/posture.
   - If posture is not green, fix the listed compliance issue first.

2. **Re-authenticate Zero Trust** from the FortiClient UI if it offers a Re-connect or
   Re-authenticate button. Do NOT restart services or reconnect the VPN without explicit approval.

3. **Switch to your usual trusted network** (office or home network) and re-check. If the ZTNA
   proxy starts there, the issue is network-specific (the endpoint can't reach the ZTNA gateway
   from the current network).

4. **Run `vpnhelper diagnose-resource --name company-bitbucket`** after re-authentication to
   confirm `FortiProxy` is now running and ZTNA listeners are present.

5. **If it persists**, send the output of `vpnhelper report` to company IT with the message in the
   section below.

---

## Message to IT (fill specifics before sending)

> While connected to the client VPN, company Bitbucket is unreachable. The FortiClient ZTNA
> access proxy (`FortiProxy`) is **not running**, and `bitbucket.<company-domain>` returns no A
> record from the current DNS path. Previously Bitbucket was reachable via ZTNA while the client
> VPN was connected. The FortiClient UI shows "Zero Trust Telemetry: Connected" but this reflects
> only the EMS management-plane connection, not the ZTNA data-plane proxy.
>
> Please check: (a) my ZTNA/EMS registration and Zero Trust posture on the endpoint, and
> (b) whether a policy, compliance, or network-based condition is preventing `FortiProxy` from
> starting. Attached: redacted diagnostic report from `vpnhelper report`.

---

## What `vpnhelper diagnose-resource` checks

```
FortiClient processes
  FortiProxy (ZTNA proxy)    : running | NOT RUNNING
  FortiTcs (ZT compliance)   : running | NOT RUNNING
  FortiESNAC (EMS mgmt)      : running | NOT RUNNING
  ...

ZTNA proxy listeners
  Port 999   : present | ABSENT
  Port 17713 : present | ABSENT
  ...

DNS resolvers (per interface — informational)
  <interface>  <servers>

DNS check
  Result : <address(es) found | no records>

TCP check (skipped if DNS failed)
  Result : connected | unreachable

Diagnosis
  Classification : <category>
  Conclusion     : <human-readable explanation>
  Next safe step : <one safe action>
```

_Tip: run `vpnhelper report report.md` to generate the full redacted diagnostic to attach to an IT ticket._
