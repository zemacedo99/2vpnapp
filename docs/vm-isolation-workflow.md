# Option C — VM isolation workflow (fallback)

Use this **only** if a company resource genuinely cannot be reached via ZTNA (Option B) and truly
needs the full company VPN at the same time as the client VPN.

> **Get approval first.** The client VPN profile may enforce device posture / compliance on the
> endpoint. Confirm with **your company IT** and **the client** that connecting their VPN from a
> virtual machine is acceptable. This document is about doing it *safely and supportedly* — it is not
> permission to do it. You need sign-off from both sides.

## Principle

Run **one** VPN inside its own operating system (a VM) so its tunnel, routes and DNS live entirely
inside the guest and cannot disturb the other context on the host.

```
            ┌─────────────────────────── Host (your managed company laptop) ────────────────────────────┐
            │  Zero Trust Fabric Agent + ZTNA  ->  company resources (source control, internal tools)     │
            │  Normal company working environment                                                         │
            │                                                                                             │
            │     ┌──────────── VMware guest (NAT / VMnet8) ───────────┐                                  │
            │     │  FortiClient  ->  client VPN profile (split tunnel) │   guest's own adapter & routes   │
            │     │  Client tools run here                              │   (host routing untouched)       │
            │     └─────────────────────────────────────────────────────┘                                │
            └─────────────────────────────────────────────────────────────────────────────────────────────┘
```

## Recommended direction: client VPN in the VM, company on the host

Keep the company context on the host because:
* the host is the **managed, Zero-Trust-attested** device — keep its posture intact;
* ZTNA and your company tooling already work on the host;
* the client engagement is cleanly sandboxed.

(The reverse — company in the VM, client on the host — also works, but then the company's Zero Trust
posture would be evaluated on a VM, which is usually undesirable and may not be permitted.)

## Networking: prefer NAT (VMnet8)

* **NAT** gives the guest its own private network behind `VMnet8`; the guest reaches the internet
  through the host's physical NIC via address translation. The guest's VPN tunnel rides its own
  virtual adapter — **the host routing table is not modified**.
* **Do not** use Bridged if isolation matters (bridged puts the guest directly on the LAN), and
  **do not** add host-only bridging, port-forwarding, or shared folders that would couple the two.
* This is the standard, supported way to run a conflicting VPN (see citations in `feasibility.md`).

### Known caveats (and fixes)
* **DNS:** VMware NAT proxies guest DNS to the host's resolvers. If that interferes with client name
  resolution in the guest, rely on the client-pushed DNS (FortiClient sets it once connected) or a
  static resolver. Keep company DNS on the host only.
* **MTU:** VPN-over-NAT can fragment. If the client tunnel is flaky in the guest, lower the guest
  tunnel MTU (e.g. 1300–1400).

## Step-by-step

1. **Create the guest** in VMware Workstation: Windows (matching the client's supported OS), give it
   enough RAM/CPU for your client tools. Set the network adapter to **NAT (VMnet8)**.
2. **Patch & secure the guest** per company/client policy (updates, disk encryption, AV as required).
   Treat it as a real managed endpoint, because it is one.
3. **Install FortiClient in the guest** and add **only** the client VPN profile (its gateway, e.g.
   `client-vpn.example.com:443`, SSO/SAML via external browser). The SAML external-browser flow works
   normally inside the guest.
4. **Connect the client VPN inside the guest.** Do your client work in the guest.
5. **On the host**, keep using ZTNA / the company environment as normal. Do not connect the company
   VPN on the host unless you actually need it for something ZTNA does not cover.
6. **Verify isolation** from the host with the helper:
   ```
   vpnhelper status
   ```
   The host should still show the company context; the host routing table should be unchanged by the
   guest's tunnel.

## What NOT to do

* Do **not** bridge guest↔host networking or forward the guest's tunnel onto the host (that defeats
  isolation and may breach policy).
* Do **not** copy client credentials/certificates between host and guest by hand. Authenticate inside
  the guest normally.
* Do **not** disable posture/compliance checks in either FortiClient to "make the VM pass". If the VM
  is not allowed, get it allowed — don't bypass the check.

## How the helper assists this workflow

The helper runs on the **host** and confirms the host context is intact and which company resources
are reachable. If you also install it in the guest, `vpnhelper status` there confirms the client
tunnel and client reachability. It does **not** automate VM creation or credentials.
