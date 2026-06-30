# Feasibility report — simultaneous company + client access

**Method:** read-only inspection only. No system, FortiClient, route, DNS, certificate, or
credential changes were made. Commands used: `ipconfig /all`, `route print -4`, `Get-NetAdapter`,
`Get-NetIPInterface`, `Get-NetRoute`, `Get-DnsClientServerAddress`, `Get-Service *Forti*`,
`Get-Process *Forti*`, `Get-NetTCPConnection`, `netsh interface show interface`,
`Get-VpnConnection`, plus DNS resolution and TCP-connect reachability checks.

> **All addresses, hostnames, machine and user names in this report are ILLUSTRATIVE PLACEHOLDERS**
> so the file is safe to share. Your real, machine-specific evidence is produced locally by
> `vpnhelper report` and is **git-ignored** — it is never committed. Placeholders used below:
> company domain `company.example`, client VPN `client-vpn.example.com`, client tunnelled range
> `172.16.0.0/12`, documentation IPs from the `203.0.113.0/24` / `198.51.100.0/24` ranges.

Environment summarised: **Windows 11 Enterprise**, **FortiClient 7.4.5** (full Fabric / Zero Trust
agent, EMS-managed), **VMware Workstation present** (NAT + host-only), single Fortinet virtual
adapter.

---

## TL;DR

> **True simultaneous access is already happening on this laptop — and it is fully supported.**
> While the client VPN tunnel was connected, a company resource (`bitbucket.company.example`, a
> private internal address) was reachable **at the same time**, served by the **Zero Trust Fabric
> Agent / ZTNA access proxy** — not by the company VPN tunnel.
>
> Two *full FortiClient VPN tunnels* (company SSL-VPN **and** client) cannot both be connected
> through the single FortiClient instance at once. But you do **not** need that: ZTNA gives
> concurrent access to company resources without occupying a VPN tunnel, so it coexists with the
> client tunnel. The only gap is **which** company resources are published as ZTNA applications.
> That is an IT configuration question, not a client limitation — and it requires **no** weakening
> of any control.

---

## 1. Environment

| Item | Value (placeholder) |
|---|---|
| OS | Microsoft Windows 11 Enterprise, 64-bit |
| Primary DNS suffix | `company.example` |
| DNS search list | `client.example`, `company.example` |
| FortiClient | 7.4.5 (`C:\Program Files\Fortinet\FortiClient\FortiClient.exe`) |
| Virtualisation present | **VMware Workstation** (NAT + host-only switches), WSL2 (Hyper-V) |
| .NET | SDK 10 (used to build the helper app; projects target net8.0) |

### FortiClient services / processes (running)

`FA_Scheduler` service running. Processes include `FortiClient`, `FortiESNAC`, `FortiGuardAgent`,
`FortiProxy`, `FortiSettings`, `FortiSSLVPNdaemon`, `FortiTcs`, `FortiTray`, `FortiVPN`, `FortiWF`.

`FortiTcs` (telemetry/compliance) and `FortiProxy` confirm this is the **full Fabric / Zero Trust
agent**, EMS-managed (consistent with the "EMS" badge in the FortiClient UI), not the free VPN-only
build.

## 2. Network adapters

Only **one** Fortinet virtual adapter exists:

| Adapter | Description | State | IPv4 (placeholder) |
|---|---|---|---|
| Ethernet 3 | **Fortinet SSL VPN Virtual Ethernet Adapter** | Up | `10.20.0.2/32` |
| WiFi | Intel Wi-Fi 6 | Up | `192.168.1.10/24` |
| Ethernet | Intel I219-V | Disconnected | — |
| VMware NAT / host-only | VMware Virtual Ethernet Adapter | Up | `192.168.56.1` / `192.168.57.1` |
| vEthernet (WSL) | Hyper-V | Up | `172.31.0.1` |
| Ethernet 2 | Other VPN adapter | Disconnected | — |

The currently-connected **client VPN** (shown with **Disconnect** + an assigned tunnel IP in the
FortiClient UI) is carried on this single `Fortinet SSL VPN Virtual Ethernet Adapter`. The company
VPN would use the **same** adapter.

## 3. Routing — the client tunnel is SPLIT tunnel

From `route print -4` with the client VPN connected (placeholder addresses):

```
0.0.0.0/0          -> 192.168.1.1    dev 192.168.1.10 (WiFi)   metric 35   <- default stays LOCAL
172.16.0.0/12      -> 10.20.0.1      dev 10.20.0.2 (tunnel)    metric 1    <- client internal
172.16.4.10/32     -> 10.20.0.1      dev 10.20.0.2 (tunnel)                <- client DNS
172.16.4.11/32     -> 10.20.0.1      dev 10.20.0.2 (tunnel)                <- client DNS
198.51.100.20/32   -> 192.168.1.1    dev 192.168.1.10          (gateway client-vpn.example.com)
203.0.113.10/32    -> ...            (FortiGate/ZTNA gateway, kept off the tunnel)
```

* **Split tunnel, not full tunnel.** Only the client internal range (`172.16.0.0/12`) is sent through
  the tunnel; the default route remains on the local Wi-Fi, so normal internet keeps working.
* `client-vpn.example.com` resolves to a public IP; a host route keeps the tunnel's own transport off
  the tunnel (expected).
* The client DNS servers are inside the tunnelled range, i.e. **reached through the tunnel** — they
  are client-side resolvers. During the investigation they also resolved company hostnames
  (`bitbucket.company.example` → a private company IP), consistent with DNS forwarding for the company
  domain; this is what lets company names resolve while the client VPN is up.
* Two **stale persistent** default routes exist but are inactive (their gateways are not on-link) —
  leftovers from prior networks. **Left untouched.**

## 4. The key finding — ZTNA is already providing concurrent company access

With the client VPN connected, reachability of `bitbucket.company.example` (a private address **not**
in the tunnelled range and **not** on the default route):

| Probe | Result | Meaning |
|---|---|---|
| ICMP ping (company internal IP) | **FAILS** (no route) | The OS has no IP route to it |
| TCP `:443` (same IP) | **SUCCEEDS** | A TCP session is established anyway |

That asymmetry (TCP works, ICMP/routing does not) is the signature of the **ZTNA access proxy**:
FortiClient intercepts TCP connections to published ZTNA destinations and forwards them over HTTPS to
the FortiGate ZTNA gateway using its EMS client certificate. Corroborating evidence:

* `FortiProxy` is **listening** on local loopback/proxy ports (the access-proxy endpoints).
* A `/32` host route to the FortiGate/ZTNA gateway is maintained even while the client tunnel is up.

So **company resources (via ZTNA) and client resources (via the VPN tunnel) are reachable at the same
time.** Verified live with the helper built in this repo (placeholder values shown):

```
Trusted network : no
VPN tunnel      : SPLIT @ 10.20.0.2
Tunneled ranges : 172.16.0.0/12, 172.16.4.10/32, 172.16.4.11/32

Company access  : Reachable
    [OK ] Bitbucket (bitbucket.company.example:443) 32ms — connected [expected: ztna]
    [OK ] Jira (jira.company.example:443) 85ms — connected [expected: ztna]
Client access   : Reachable
    [OK ] Client DNS (172.16.4.10:53) 58ms — connected [expected: vpn]

Notes:
  • FortiClient tunnel active (SPLIT tunnel, 3 routed range(s)).
  • Guidance: both company and client resources are reachable right now — no switching needed.
```

(`[expected: …]` is a config label; the tool measures reachability, not the path taken. A company
host being reachable while *outside* the tunnelled ranges is what points to ZTNA. A SaaS host like
`jira.company.example` may instead be reachable directly over the internet — confirm per-resource
with IT, the tool cannot tell the path apart.)

## 5. Answers to the Phase 0 questions

| Question | Finding |
|---|---|
| OS version | Windows 11 Enterprise, 64-bit |
| Active adapters | Wi-Fi, Fortinet SSL-VPN (tunnel), VMware NAT/host-only, WSL vEthernet |
| Forti adapters/services/processes | One `Fortinet SSL VPN Virtual Ethernet Adapter`; full Fabric agent (FortiTcs, FortiProxy, FortiSSLVPNdaemon, …) running, EMS-managed |
| Routes before/after | Client VPN connected = split tunnel (client range + 2× `/32` DNS via tunnel; default stays on Wi-Fi). |
| Can both VPNs exist as separate tunnel interfaces? | **No** — there is a single Fortinet virtual adapter and the FortiClient UI connects one profile at a time. (Server-side dual **IPsec IKEv2** tunnels exist in 7.4.4+ via *Multi-Connect*, but not for SSL-VPN, not for the SSL+IPsec mix, and not across two organisations' EMS — see §6.) |
| Does FortiClient enforce a single active tunnel globally? | **Effectively yes here** — primarily because there is one shared Fortinet virtual adapter and the client connects one profile at a time. A FortiGate "Limit users to one SSL-VPN connection at a time" portal setting can reinforce it server-side. |
| Split tunneling enabled/disabled? | **Enabled** for the client profile (client internal range only). |
| Could the client IPsec/SAML profile use the built-in Windows VPN? | **No.** The native Windows client cannot do FortiGate SSL-VPN, and cannot perform SAML SSO via an external browser. The client VPN must use FortiClient. (Plain IPsec IKEv2 with certificate/EAP *could* use native Windows, but this profile uses SAML/external-browser.) |
| Could a VM isolate one VPN while the host keeps the other? | **Yes.** VMware Workstation is installed; a NAT'd guest can run one FortiClient tunnel independently of the host. See `vm-isolation-workflow.md`. |

## 6. Verified external facts (with citations)

Checked against official Fortinet / Microsoft / VMware documentation.

1. **FortiClient single concurrent tunnel — *nuanced*.** FortiClient **7.4.4+** *does* support
   concurrent **IPsec IKEv2** tunnels via *Multi-Connect*, but **only** for EMS-managed IPsec
   profiles — **not** SSL-VPN, and Fortinet does not document mixing SSL-VPN + IPsec. The
   "second connection disconnects the first" behaviour for SSL-VPN comes from the FortiGate portal
   option *"Limit Users to One SSL-VPN Connection at a Time"*. Net effect for *this* SSL-VPN +
   cross-org-IPsec setup: not user-achievable as a native FortiClient dual tunnel.
   <br>Sources: docs.fortinet.com `forticlient/7.4.5/ems-administration-guide/963806/dual-ipsec-vpn-tunnel-support`; community.fortinet.com SSL-VPN concurrency threads.
2. **ZTNA without a VPN tunnel — supported.** Fortinet ZTNA TCP-forwarding / access proxy reaches
   internal apps "without needing to form IPsec or SSL VPN tunnels … eliminating the use of dial-up
   VPNs", via FortiClient acting as a local proxy gateway using an EMS-issued certificate. ZTNA is
   documented as *augmenting* VPN, i.e. it coexists.
   <br>Sources: docs.fortinet.com `fortigate/7.0.0/ztna-architecture/19197/ztna-access-proxy`, `forticlient/7.4.7/ems-administration-guide/543857/ztna-destinations`, `fortigate/7.4.4/administration-guide/735065/augmenting-vpn-security-with-ztna-tags`.
3. **Native Windows VPN cannot do FortiGate SSL-VPN or SAML/external-browser** (only IKEv2/L2TP/
   SSTP with EAP/cert/PSK). SAML-with-external-browser is FortiClient-specific.
   <br>Sources: learn.microsoft.com `.../vpn/vpn-connection-type`, `.../vpn/vpn-authentication`; docs.fortinet.com `forticlient/7.4.7/ems-administration-guide/712604/ipsec-vpn-saml-based-authentication`.
4. **VMware Workstation NAT isolates a guest VPN from the host** (own virtual NIC, host routing
   untouched), the standard way to run a conflicting VPN. Caveats: NAT DNS is proxied via the host's
   resolvers, and VPN-over-NAT often needs a lower MTU.
   <br>Sources: techdocs.broadcom.com Workstation Pro 17 NAT docs.

## 7. Conclusion

* **Simultaneous access is achievable today and is already partly in place** via ZTNA + the client
  VPN — no policy bypass required.
* **What likely causes the "switching" pain:** some company resources are not (yet) published as ZTNA
  applications, so for those you still reach for the full company VPN, which conflicts with the client
  tunnel.
* **Recommended direction** (detailed in `architecture.md`): ask IT to extend ZTNA coverage
  (Option B) — the cleanest supported fix — with VM isolation (Option C) as the no-server-change
  fallback. A host-side route-manipulation app (Option A) is **not viable and not built**, because
  the two tunnels never coexist on the host to arbitrate between.
