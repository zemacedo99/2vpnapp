# FortiClient capabilities (as observed + documented)

Read-only findings for this endpoint, plus the documented Fortinet behaviour that governs dual-VPN.
Placeholders used for any environment-specific value.

## Installed client

| Item | Value |
|---|---|
| Product | FortiClient (full Fabric / Zero Trust agent) |
| Version | 7.4.5 |
| Running components | FortiTcs (telemetry/compliance), FortiProxy (ZTNA access proxy), FortiSSLVPNdaemon, FortiVPN, FortiESNAC, FortiWF, FortiTray, … |
| Management | EMS/telemetry integration present (`FA_ESNAC`, `emscert_list`, `cloud_client` keys) — endpoint is centrally managed |
| ZTNA | multiple ZTNA rules configured (`FA_ZTNA\rules\rule0…ruleN`); access proxy active |

## Configured VPN profiles (types)

Determined from registry key **names** only (no secret values read):

| Container | Result |
|---|---|
| `...\FortiClient\Sslvpn\Tunnels` | **several SSL-VPN profiles** (company + client + variants) |
| `...\FortiClient\IPSecVPN\Tunnels` | **empty — 0 IPsec tunnels** |

→ Every profile the user needs is **SSL-VPN**.

## Virtual adapters

| Adapter | Notes |
|---|---|
| `Fortinet SSL VPN Virtual Ethernet Adapter` | **single** SSL-VPN adapter; shared by all SSL-VPN profiles; carries the one active tunnel |
| (no IPsec adapter present) | consistent with 0 IPsec tunnels |

## Concurrency / Multi-Connect

- **SSL-VPN: one tunnel at a time.** FortiClient holds a single SSL-VPN session; connecting another
  displaces the current one. This is architectural (single shared adapter), not a togglable setting.
- **Multi-Connect (Dual IPsec VPN tunnel support), 7.4.4+:** multiple concurrent tunnels for
  **EMS-managed IPsec IKEv2** only, enabled via the EMS "Enable Multi-Connect on Eligible Tunnels"
  toggle. **Does not cover SSL-VPN**, and there is no documented SSL-VPN + IPsec concurrent mix.
- **Applicability here: none** — there are no IPsec tunnels, and both needed profiles are SSL-VPN.
- Citations: docs.fortinet.com `forticlient/7.4.5/ems-administration-guide/963806/dual-ipsec-vpn-tunnel-support`; community.fortinet.com SSL-VPN concurrency thread.

## Command-line interface

- A **supported Windows connect CLI exists in 7.4.6+**: `FortiVPN.exe --cli --connect --tunnel <name>`
  (+ `--list`, `--status`, `--disconnect`). The 7.4.0–7.4.5 Windows CLI documentation lists only
  `FortiESNAC.exe` (EMS registration/telemetry) — i.e. it is **not documented for this 7.4.5 build**.
- Even where present, the connect CLI: (a) cannot create profiles (must pre-exist), (b) still connects
  **one** SSL-VPN tunnel at a time (does not enable dual), and (c) accepts `--username/--password/
  --savecredentials`. Because handling credentials/MFA is explicitly out of scope, **this app does not
  invoke `FortiVPN.exe --connect`** — `connect-both`/`disconnect` are guided-only.
- Citations: docs.fortinet.com `forticlient/7.4.7/administration-guide/95591/forticlient-windows-cli-commands`, `forticlient/7.4.5/administration-guide/95591/forticlient-windows-cli-commands`.

## Native Windows VPN (for completeness)

The built-in Windows client supports IKEv2/L2TP/SSTP/PPTP with EAP/certificate/PSK — **no SSL-VPN, no
SAML/external-browser**. The client profile (SSL-VPN + SAML external browser) therefore cannot be
re-hosted natively; this is architectural, not a matter of missing secrets. Citations:
learn.microsoft.com `.../vpn/vpn-connection-type` & `.../vpn/vpn-authentication`.

## Net effect

The only concurrent, host-only mechanism available on this endpoint is **ZTNA + one VPN**, not two VPN
tunnels. See `host-only-dual-vpn-feasibility.md` and `limitations.md`.
