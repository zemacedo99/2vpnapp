# IT request template (Option B — extend ZTNA coverage)

A generic, ready-to-fill request you can send to your **company IT / Security**. It asks only for
**supported configuration** — never to weaken a control. Fill the `[bracketed]` placeholders with
your specifics before sending. `vpnhelper it-request` produces a version pre-filled with your live
(redacted) reachability evidence.

> Placeholders to replace: `[Company]`, `[Client]`, `[client VPN profile name]`, and the resource
> list. Keep your real internal hostnames/IPs out of any copy you store in the repo — put them only
> in the message you actually send.

---

**To:** [Company] IT / Security
**From:** [your name]
**Subject:** Request to extend ZTNA coverage so I can keep the client VPN connected

Hello,

I work on the **[Client]** engagement and connect to their network with our FortiClient client VPN
profile (**[client VPN profile name]**, SAML SSO). I also need several **[Company]** resources during
the same working sessions.

**1. The client VPN must stay connected for client work.** Disconnecting it to reach a company
resource, then reconnecting, is the friction I'm trying to remove. The client VPN is a split tunnel,
so it does not carry my general traffic — only the client's internal ranges.

**2. Company access via ZTNA already works alongside the client VPN.** With the client VPN connected,
I can already reach some company resources at the same time — for example company **Bitbucket** /
source control. This is the **Zero Trust Fabric Agent / ZTNA access proxy** working concurrently with
the client tunnel (it reaches internal apps without forming a company VPN tunnel), which is exactly
the behaviour I want.

**3. Please publish the remaining required company resources through the same supported ZTNA model.**
The resources that still force me to disconnect the client VPN and connect the full company VPN are:

- [resource 1 — name + the internal URL/host:port, filled in the message you send]
- [resource 2 — …]
- [add the specific internal tools you need]

Publishing these as **ZTNA applications** on the FortiGate ZTNA access proxy (EMS-managed), the same
way Bitbucket appears to be, would let me reach them via the Fabric Agent without the company SSL-VPN
tunnel — so I can keep the client VPN connected and stop switching.

**4. I am NOT requesting any bypass or weakening.** Specifically I am **not** asking to:
- bypass, disable, patch, or reverse-engineer FortiClient, Zero Trust, or endpoint protection;
- change split-tunnel routing, add manual routes, or alter VPN profiles myself;
- weaken MFA, SAML, certificate checks, firewall policy, or device posture/compliance.

ZTNA is *stronger* than a broad VPN tunnel — per-application, certificate- and posture-checked access
— so this request increases security posture rather than reducing it.

**If a specific resource cannot be ZTNA-published**, I'd like to discuss the approved alternative of
running the **client VPN inside a NAT-isolated virtual machine** while my host keeps the company
context (details: `docs/vm-isolation-workflow.md`). Please confirm whether that is acceptable under
company and client policy, since the client VPN may check device posture.

**Evidence (read-only, secrets redacted).** I've attached a diagnostic report from a read-only helper
(`vpnhelper report`): OS/FortiClient versions, the active client tunnel (split/full + routed ranges),
which company resources are reachable alongside the client VPN, which are not, and any routing
overlaps. It contains no credentials, tokens, cookies, SAML assertions, or private keys.

Happy to jump on a quick call. Thank you!

---

### Notes for whoever edits this
* Keep the tone "please add supported access", never "please turn off X".
* `vpnhelper it-request` pre-fills the reachable / not-reachable lists and the evidence bullets from
  your `config.json` targets; the not-reachable list is what you're asking to publish.
* Attach the output of `vpnhelper report` (already redacted) rather than raw command dumps.
* The tool checks reachability, not the path taken — so confirm with IT which reachable resources are
  actually ZTNA vs. public/cloud before describing them.
