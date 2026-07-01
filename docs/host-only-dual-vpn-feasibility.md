# Host-only dual-VPN feasibility

**Question:** can a Windows host-only app connect and maintain **both** VPN contexts (company + client)
at the same time, from the normal desktop session — no VM, no WSL, no second machine?

**Method:** read-only investigation only (registry key *names*, adapters, routes, services — no secret
values read, no system state changed).

> All addresses, hostnames, machine/user names in this document are **illustrative placeholders** so
> it is safe to share. Real values live only in the git-ignored `config/config.json`.

---

## Verdict: NOT POSSIBLE as a host-only app (this is "Option D")

Two simultaneous FortiClient tunnels **cannot** be established on this host, and no host-only app can
change that without an unsupported workaround (which is out of scope). The blocker is **profile type +
FortiClient architecture**, not missing code:

1. **Both VPN profiles are SSL-VPN.** The registry shows all configured tunnels under
   `...\FortiClient\Sslvpn\Tunnels` (several SSL-VPN profiles) and the `...\IPSecVPN\Tunnels` container is
   **empty** (0 IPsec tunnels).
2. **FortiClient connects only one SSL-VPN tunnel at a time.** All SSL-VPN profiles share a single
   `Fortinet SSL VPN Virtual Ethernet Adapter`; starting a second SSL-VPN session displaces the first.
3. **Multi-Connect does not apply.** The only Fortinet concurrent-tunnel feature ("Dual IPsec VPN
   tunnel support" / Multi-Connect, 7.4.4+) is for **EMS-managed IPsec IKEv2** tunnels only — not
   SSL-VPN, and not an SSL-VPN + IPsec mix. With zero IPsec tunnels, it is categorically inapplicable.
4. **The client profile uses SSL-VPN + SAML external-browser SSO**, which the built-in Windows VPN
   client cannot host (so "move one VPN to native Windows" is impossible without gateway reconfig, and
   even with extracted secrets — which are off-limits — the native client speaks neither SSL-VPN nor
   SAML/external-browser).

`vpnhelper connect-both` reports this verdict from the live machine and exits non-zero.

## The supported host-only path that DOES exist (and is already running)

**ZTNA (Zero Trust Network Access)** reaches internal applications **without a VPN tunnel** and runs
**concurrently with the client VPN**. This host already has **multiple ZTNA rules** configured
(`...\FA_ZTNA\rules\rule0…ruleN`) and the ZTNA access proxy (`FortiProxy`) running. So:

- Keep the **client VPN** connected for client work.
- Reach **company** resources that are **published as ZTNA applications** at the same time — no switch.
- Use `vpnhelper status` to see, per resource, what is reachable right now.

### Important limit for "internal VLAN / lab IPs"
ZTNA is **per-published-destination** (each rule is a `host/FQDN:port`), and TCP-forwarding carries
**TCP only** (no ICMP/ping, no arbitrary subnet). So:

- A lab/VLAN **service** reachable at a specific `host:port` **can** be published as a ZTNA rule and
  then used concurrently with the client VPN.
- **Arbitrary** lab IP access (whole subnets, ping, non-TCP, ad-hoc hosts) is **not** something ZTNA
  provides; that still needs the full company SSL-VPN tunnel — which conflicts with the client VPN.

So the honest per-resource rule is: **if the company lab resource can be expressed as `host:port`, ask IT
to publish it via ZTNA (concurrent, supported). If it genuinely needs raw L3/subnet access, it needs
the full company tunnel, and there is no host-only way to have that at the same time as the client
VPN** — the only supported options are switching, or (excluded by your constraints) a VM.

## What would need to change to get true host-only dual tunnels

| Change | Owner |
|---|---|
| Re-provision **both** VPNs server-side as **IPsec IKEv2** (not SSL-VPN) | Company IT **and** Client IT (VPN gateways) |
| Manage both under **one EMS** with **"Enable Multi-Connect on eligible tunnels"** | A single EMS authority for both — impractical across two organisations |

This is a server/EMS re-architecture spanning two companies. It is not something a host-only app can
do, and this project does not attempt it.

## Verified external facts (with citations)

1. **SSL-VPN is single-connection; Multi-Connect is IPsec IKEv2-only (7.4.4+).**
   docs.fortinet.com `forticlient/7.4.5/ems-administration-guide/963806/dual-ipsec-vpn-tunnel-support`;
   Fortinet Community "Multiple different FortiClient SSL-VPN connections at the same time".
2. **A supported Windows connect CLI exists — but only in 7.4.6+, and it connects one tunnel.**
   `FortiVPN.exe --cli --connect|--disconnect|--list|--status`. Documented for 7.4.6/7.4.7; the 7.4.0–
   7.4.5 CLI pages document only `FortiESNAC.exe`. It also accepts `--username/--password`, so this app
   deliberately does **not** drive it (no credential handling), and it cannot create a second concurrent
   SSL-VPN tunnel anyway. docs.fortinet.com `forticlient/7.4.7/administration-guide/95591/forticlient-windows-cli-commands` (and 7.4.5 same path for contrast).
3. **Native Windows VPN cannot host SSL-VPN or SAML/external-browser** (only IKEv2/L2TP/SSTP/PPTP with
   EAP/cert/PSK). learn.microsoft.com `.../vpn/vpn-connection-type`, `.../vpn/vpn-authentication`;
   docs.fortinet.com `.../using-a-browser-as-an-external-user-agent-for-saml-authentication-in-an-ssl-vpn-connection`.
4. **ZTNA is per-published-destination, coexists with a VPN, TCP-only (no L3/ICMP).**
   docs.fortinet.com `fortigate/7.0.0/ztna-architecture/19197/ztna-access-proxy`,
   `forticlient/7.4.4/administration-guide/403758/ztna-destination`.

## Recommendation

- Day-to-day: keep the client VPN up and use ZTNA for company access; run `vpnhelper status`.
- For each company resource that is unreachable concurrently, run `vpnhelper it-request` and ask IT to
  publish it as a ZTNA application (`docs/it-request-template.md`).
- Do **not** pursue any host-only dual-tunnel workaround — see `docs/limitations.md`.
