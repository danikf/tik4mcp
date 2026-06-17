---
name: mikrotik-firewall
description: Understand, audit, and safely change the MikroTik RouterOS firewall via tik4mcp. Use when the user wants to read/explain firewall rules, review or harden the firewall, add/modify/reorder filter or NAT or mangle or RAW rules, set up masquerade or port-forwards, manage address-lists, or wants firewall best-practice recommendations.
---

# MikroTik firewall — understand, advise, change

Drive the firewall through the tik4mcp tools (`mikrotik_command` for reads and all writes; the curated
read tools for quick looks). This skill carries the **logic and best practices**; for exact field
semantics and the current authoritative ruleset, consult the MikroTik docs linked below — prefer them
over memory, since RouterOS details change between versions.

## Mental model (explain this when asked to "understand" the firewall)

- **Tables**: `filter` (allow/deny), `nat` (address translation), `mangle` (marking), `raw`
  (pre-connection-tracking drops). Plus `/ip/firewall/address-list` and connection tracking.
- **Chains**: `input` = traffic *to the router itself*; `forward` = traffic *through* the router
  (LAN↔WAN); `output` = traffic *from* the router. NAT uses `srcnat`/`dstnat`.
- **Evaluation**: rules run **top-down, first match wins**, per chain. **Order is everything** — a
  rule's effect depends entirely on what precedes it.
- **Connection state**: each packet is `new` / `established` / `related` / `invalid` / `untracked`.
  Accepting `established,related,untracked` early and dropping `invalid` is the backbone of a stateful
  firewall.

To read & explain a firewall: `mikrotik_command` with `/ip/firewall/filter/print`,
`/ip/firewall/nat/print`, `/ip/firewall/mangle/print`, `/ip/firewall/raw/print`,
`/ip/firewall/address-list/print`, `/ip/firewall/connection/print`. Walk the rules in order and
describe what each does and what reaches the bottom.

## MikroTik's recommended baseline (best practices)

Mirror MikroTik's recommended ruleset (see [Firewall & QoS case studies](https://manual.mikrotik.com/docs/firewall-and-quality-of-service/firewall-and-qos-case-studies/), incl. "Building Advanced Firewall"):

- **Use interface lists** `WAN` and `LAN` (`/interface/list` + `/interface/list/member`) and match on
  `in-interface-list`/`out-interface-list` instead of raw interface names — rules survive topology
  changes.
- **`input` chain**: accept `established,related,untracked` → drop `invalid` → accept ICMP (don't
  over-filter it) → accept from `LAN` (and/or a management address-list) → **drop everything else**.
- **`forward` chain**: FastTrack `established,related` (IPv4, big performance win) → accept
  `established,related,untracked` → drop `invalid` → accept LAN→WAN → drop WAN traffic that isn't
  dst-NATed → drop bogon sources → **drop the rest**.
- **`raw`/prerouting**: drop bogons/reserved ranges and obviously bad packets *before* conntrack to
  save CPU under load.
- **NAT**: use `action=masquerade` for a **dynamic** WAN address; use `action=src-nat` only for a
  static public IP. Port-forwards are `chain=dstnat action=dst-nat`.
- **Don'ts**: never omit the final "drop the rest" (protects newly-added interfaces); don't FastTrack
  IPsec traffic without a policy-bypass rule; don't expose router services to `WAN`.

## Changing the firewall — SAFETY FIRST

A wrong rule can **lock you out instantly**. Always:

1. **Confirm with the user** before any filter/NAT change; restate the intended effect.
2. **Protect your own access first.** If managing remotely, ensure an `accept` rule for your
   management source precedes any new `drop`. (RouterOS **Safe Mode** would auto-revert on a dropped
   session, but it only spans a *single* session — it does **not** carry across separate
   `mikrotik_command` calls, so don't rely on it here; see the safe-mode note in `mikrotik-admin`.)
3. **Order matters — be explicit.** New filter rules append to the end by default (often *after* a
   drop, so they never match). Use `=place-before=<id>` on add, or `/ip/firewall/filter/move` with
   `=numbers=<id> =destination=<pos>`. Always re-print and verify order after changing.
4. **Prefer reversible steps**: add new rules with `=disabled=yes`, verify, then
   `/ip/firewall/filter/enable`. Disable rather than remove while testing.

Example — add a stateful baseline accept at the top of `input`:
`/ip/firewall/filter/add` · `=chain=input` · `=action=accept` ·
`=connection-state=established,related,untracked` · `=place-before=0` · `=comment=baseline est/rel`

Example — masquerade for a dynamic WAN:
`/ip/firewall/nat/add` · `=chain=srcnat` · `=action=masquerade` · `=out-interface-list=WAN`

## Audit / recommendations checklist

When asked to review or harden, report findings against these:
- `input` ends in `drop` (no implicit accept exposure)? `invalid` dropped? router services not reachable from `WAN`?
- Stateful accept present and **near the top** of both chains? FastTrack enabled on `forward`?
- Masquerade vs src-nat correct for the WAN type? Any overly broad `dst-nat`/port-forwards?
- Management restricted to an address-list? Useful rules disabled or shadowed by an earlier match?
- Address-lists current; logging on key drops for visibility.

## Reference (MikroTik docs — manual.mikrotik.com)

- [Firewall & QoS (overview)](https://manual.mikrotik.com/docs/firewall-and-quality-of-service/) ·
  [Packet Flow in RouterOS](https://manual.mikrotik.com/docs/firewall-and-quality-of-service/packet-flow-in-routeros)
- [Filter](https://manual.mikrotik.com/docs/firewall-and-quality-of-service/firewall/filter) ·
  [NAT](https://manual.mikrotik.com/docs/firewall-and-quality-of-service/firewall/nat) ·
  [Mangle](https://manual.mikrotik.com/docs/firewall-and-quality-of-service/firewall/mangle) ·
  [Address-lists](https://manual.mikrotik.com/docs/firewall-and-quality-of-service/firewall/address-lists)
- [Common matchers & actions](https://manual.mikrotik.com/docs/firewall-and-quality-of-service/firewall/common-firewall-matchers-and-actions) ·
  [Connection tracking](https://manual.mikrotik.com/docs/firewall-and-quality-of-service/connection-tracking) ·
  [Case studies (incl. Building Advanced Firewall)](https://manual.mikrotik.com/docs/firewall-and-quality-of-service/firewall-and-qos-case-studies/)

See the `mikrotik-admin` skill for transports, the router inventory, and the global safety rules.
