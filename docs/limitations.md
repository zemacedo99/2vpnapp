# Limitations & why a host-only dual-VPN app cannot safely do it

## Hard limits (evidence-backed)

| # | Limit | Consequence |
|---|---|---|
| 1 | Both required profiles are **SSL-VPN**; FortiClient holds **one** SSL-VPN tunnel at a time (single shared adapter). | Cannot have both company + client tunnels up at once. |
| 2 | **Multi-Connect** (the only concurrent-tunnel feature) is **EMS-managed IPsec IKEv2 only**; there are **0 IPsec tunnels**. | The feature that could allow dual tunnels does not apply. |
| 3 | Native Windows VPN cannot host **SSL-VPN** or **SAML/external-browser**. | Can't offload one VPN to native Windows (even with secrets, which are off-limits). |
| 4 | **ZTNA** is **per-published-destination** and **TCP-only**. | Concurrent company access works only for resources IT has published as ZTNA rules; arbitrary lab/VLAN L3/ICMP access is not covered. |
| 5 | User constraints exclude **VM / WSL / second machine / RDP / SSH / SOCKS / manual switching**. | The otherwise-viable isolation fallback (a NAT'd VM) is off the table. |
| 6 | The 7.4.6+ connect CLI (`FortiVPN.exe --cli`) handles **credentials** and connects **one** tunnel. | Cannot be used to build dual access, and driving it would breach the no-credentials rule. |

## Stop condition — what this project will NOT do

Achieving host-only dual VPN in this setup would require one of the following, all of which are
**out of scope**:

- Patching, tampering with, reverse-engineering, or hooking FortiClient / its adapter / driver / service.
- Extracting the SSL-VPN/SAML configuration or secrets from FortiClient to feed another client.
- Disabling or weakening EMS policy, Zero Trust, endpoint protection, MFA, SAML, or certificate checks.
- Creating an unauthorized second tunnel, or bridging/forwarding company↔client traffic.
- Injecting routes to a network you are not tunnelled into.

None of these are implemented. Where dual connection is impossible without them, the tool **documents
why** instead of doing a workaround.

## The legitimate path (recap)

- **Supported concurrent access:** ZTNA (for published company resources) alongside the client VPN.
  This host already has multiple ZTNA rules and the access proxy running.
- **To extend coverage:** ask IT to publish the specific company resources you need (each as a
  `host:port` ZTNA application) — `vpnhelper it-request`, template in `it-request-template.md`.
- **True dual tunnels:** only via a cross-company IPsec IKEv2 + single-EMS Multi-Connect
  re-architecture (Company IT **and** Client IT) — impractical, and not a host-only app's job.

## What the app safely provides

- `status` — trusted-network, active tunnel (split/full + ranges), per-resource reachability.
- `connect-both` — **analysis only**: prints the dual-VPN verdict + who would need to change what.
  Never establishes a tunnel, never touches credentials/MFA.
- `disconnect` — explains the supported way to disconnect (FortiClient UI); no automation.
- `dry-run-routes` — read-only route/DNS conflict analysis; applies nothing; no rollback needed.
- `report` / `it-request` — redacted diagnostic + ready-to-send ZTNA request.

All read-only; no admin; no secrets stored or logged.
