# Recommended architecture

This evaluates the four options from the brief **in order**, against the evidence in
[`feasibility.md`](feasibility.md), and states what was built and why.

## Decision summary

| Option | Verdict | Why |
|---|---|---|
| **A** — host dual-routing tray app | ❌ Not viable, not built | The two FortiClient tunnels never coexist on the host, so there are no two live interfaces to route between. Injecting routes to a network you are not tunnelled into would also be a policy bypass. |
| **B** — ZTNA / IT configuration | ✅ **Recommended (primary)** | ZTNA already provides concurrent company access alongside the client VPN. Extending ZTNA coverage to the remaining company apps removes all switching, with zero control-weakening. |
| **C** — approved VM isolation | ✅ Recommended fallback | VMware is installed; a NAT'd guest runs one VPN independently of the host. Use if a company resource genuinely cannot be ZTNA-published and needs the full company VPN concurrently. |
| **D** — productivity helper app | ✅ **Built** | A safe, read-only tray app + CLI that shows live context, tells you when/whether you must switch, and generates the Option-B IT request and a redacted diagnostic report. |

> Net recommendation: **Pursue B, use C as fallback, and run D day-to-day** to know exactly which
> resources need attention and to produce the evidence for B.

---

## Option A — host dual-routing — NOT VIABLE (and not built)

The brief's Option A assumes both VPN tunnel interfaces can coexist so a tray app can route specific
subnets to the correct tunnel. The investigation shows they **cannot**: there is a single Fortinet
virtual adapter and FortiClient connects one profile at a time (§2, §5 of feasibility). With only one
tunnel ever live, there is nothing to arbitrate.

Equally important on safety grounds: making a company subnet reachable by **adding routes** while you
are *not* connected to the company VPN would mean routing that traffic somewhere it should not go —
a policy bypass. So this option is deliberately **not implemented**. The helper app (D) will *show*
routes and can write a dry-run plan + rollback file for transparency, but it **never applies route
changes**.

## Option B — ZTNA / supported IT configuration — RECOMMENDED

ZTNA (Zero Trust Network Access) is Fortinet's model for reaching internal applications **without a
VPN tunnel**. The Fabric Agent intercepts connections to *published* destinations and forwards them
over HTTPS to the FortiGate ZTNA gateway using an EMS-issued client certificate. Because it does not
occupy a VPN adapter, **it runs concurrently with the client tunnel** — which is exactly what we
observe live for a company internal host (e.g. Bitbucket) today.

**The ask to IT** (generate the filled-in version with `vpnhelper it-request`, template in
[`it-request-template.md`](it-request-template.md)):

> Please publish the remaining company resources I need (e.g. the specific internal tools that
> currently require the full company VPN) as **ZTNA applications** on the FortiGate ZTNA access proxy,
> EMS-managed, as already appears to be done for Bitbucket. This lets me reach them via the Zero Trust
> Fabric Agent without the SSL-VPN tunnel, so I can keep the client VPN connected and stop switching.

Why this is the best option:
* **No control is weakened.** ZTNA adds per-application, certificate- and posture-checked access — it
  is *stronger* than a broad VPN tunnel, not weaker.
* **Already proven on this machine** for at least Bitbucket.
* **Nothing to install or maintain** on the endpoint beyond the existing agent.

If ZTNA coverage cannot be extended for a given resource, fall back to Option C for that resource.

### Note on a FortiClient-native dual tunnel
FortiClient 7.4.4+ supports concurrent **IPsec IKEv2** tunnels (*Multi-Connect*, EMS-managed). It is
**not** applicable here because the company side is SSL-VPN, the combination is SSL+IPsec, and the
two VPNs belong to different organisations' EMS. Mention it to IT only as a distant alternative.

## Option C — approved VM isolation — RECOMMENDED FALLBACK

Run **one** VPN inside a VMware NAT'd guest so it cannot disturb the other context on the host.
Full step-by-step in [`vm-isolation-workflow.md`](vm-isolation-workflow.md). Summary:

* **Recommended direction:** keep the **company** context on the **host** (Zero Trust
  posture/attestation stays on the managed host; ZTNA + company tooling stay where they belong) and
  run the **client VPN profile inside the VM**.
* **Networking:** prefer **NAT (VMnet8)** — the guest gets its own adapter and its tunnel never
  touches the host routing table. Do **not** bridge or forward traffic between guest and host.
* **Required confirmation:** the client VPN may enforce **device posture/compliance** on the
  endpoint; confirm with both company IT and the client that connecting from a VM is acceptable before
  relying on this. This is a policy question, not a technical blocker.

Trade-offs: a VM costs RAM/disk and a second login; NAT DNS is proxied via the host's resolvers and
VPN-over-NAT may need a lower MTU (the guide covers this).

## Option D — productivity helper — BUILT

Because simultaneous access is *possible* but **conditional** (depends on which resources are
ZTNA-published), the single most useful safe tool is one that makes the current state obvious and
generates the Option-B evidence. That is what this repo builds:

* **`VpnContextHelper` tray app** (WPF + tray icon) and **`vpnhelper` CLI**, sharing one verified
  core library.
* Detects, read-only: trusted/on-net, the active FortiClient tunnel (split vs full, routed ranges),
  and configurable reachability of company (ZTNA) and client (VPN) resources.
* Tells you plainly: *both reachable — no switching needed* / *company resource X unreachable — ask
  IT to ZTNA-publish it, or switch* / *connect the client VPN*.
* **Opens FortiClient** (UI only — never enters credentials/MFA/SAML).
* Generates a **redacted diagnostic report** and a **pre-filled IT request** for Option B.
* Flags **route/addressing overlaps** between the client tunnel and configured company ranges.

### Safety properties (by construction)
* **Read-only.** No `route add/delete`, no VPN connect/disconnect, no profile/credential/cert
  changes. The only write actions are saving the report/IT-request files you ask for, and the log.
* **No secrets handled.** Reachability uses TCP-connect/DNS only (no payload read). All report/log
  text passes through a redactor that strips credentials, tokens, cookies, PSKs, SAML assertions and
  PEM blocks, and masks MAC addresses / your username — while preserving routing evidence.
* **No admin rights.** Manifest requests `asInvoker`. Everything it does works as a normal user.
* **Config-driven.** All hostnames/subnets live in `config/config.json`; nothing is hardcoded.

## What would change the recommendation
* If IT confirms a company resource **cannot** be ZTNA-published → use Option C (VM) for that
  resource.
* If both organisations agree to EMS-managed **IPsec IKEv2 Multi-Connect** profiles → a native
  FortiClient dual tunnel becomes possible, superseding C. Unlikely across two orgs.
