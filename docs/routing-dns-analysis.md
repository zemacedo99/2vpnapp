# Routing / DNS analysis

Per the brief, a split-routing/DNS plan is only relevant **if both VPNs can be legitimately connected
at the same time**. They cannot on this host (see `host-only-dual-vpn-feasibility.md`), so:

> **No route or DNS change is proposed or applied. There is no second tunnel to split-route toward,
> and this project never routes one VPN's traffic into the other, never bridges the two networks, and
> never adds a route to reach a network you are not tunnelled into.**

`vpnhelper dry-run-routes` therefore reports **0 planned mutations** and applies nothing.

## What the tool analyses (read-only)

Even without any change to apply, the helper performs read-only conflict analysis that *would* be the
prerequisite of any safe split-routing — surfacing problems rather than acting on them:

### 1. Subnet / route overlap
`RouteDnsAnalyzer.FindSubnetConflicts(companyCidrs, activeTunnelCidrs)` flags any configured company
range that is *captured* by the active tunnel's routed ranges (the default route `0.0.0.0/0` is
excluded — a full tunnel legitimately carries everything). Overlap would mean the active tunnel is
shadowing a company range.

Observed on this host (placeholder values): active tunnel is **split** (carries only the client's
internal range + its two DNS `/32` routes); the default route stays on the local LAN; **no overlap**
with the configured company ranges was detected.

### 2. DNS suffix conflict
`RouteDnsAnalyzer.FindDnsSuffixConflicts(assignments)` flags the same DNS **suffix** advertised on two
interfaces with **different** resolver sets — which would make resolution of that suffix ambiguous /
order-dependent if two contexts were active. This is detection only.

## Safety properties of any (hypothetical) route work

Encoded as hard rules in the design, even though nothing is applied:

- **Dry-run first** — `dry-run-routes` prints the plan and applies nothing.
- **No cross-routing** — never send company traffic via the client tunnel or vice-versa.
- **No bridging/forwarding** between the two networks.
- **Explicit + reversible** — any real mutation would require explicit confirmation and a rollback
  file; since dual connection is impossible here, none is offered.
- **Read-only by default** — the tool's normal operation (`status`, `report`, `dry-run-routes`) never
  changes routes, DNS, services, or firewall.

## Why "just add routes" is not a solution

Making a company subnet reachable by injecting routes while the company VPN is **not** connected would
send that traffic somewhere it cannot go (or must not go) — it does not create access, and it risks
policy violation. Reachability to a company network requires that network's tunnel (or a ZTNA rule for
a specific destination), not a local route edit.
