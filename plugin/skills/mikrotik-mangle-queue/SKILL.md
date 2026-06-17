---
name: mikrotik-mangle-queue
description: Set up MikroTik traffic shaping / QoS via tik4mcp — mangle connection & packet marking plus queues (simple queues and hierarchical queue trees with the right queue type, e.g. PCQ/HTB). Use when the user wants to limit or prioritize bandwidth, mark traffic, build a queue tree hierarchy, do per-user fairness, or understand/audit existing mangle and queues.
---

# MikroTik mangle & queues (QoS / traffic shaping)

Drive everything through tik4mcp (`mikrotik_command` for reads and all writes). This skill carries the
QoS *logic*; for field semantics and version specifics, prefer the MikroTik docs linked below.

## Always read the topology first

Shaping decisions depend on the addressing and interfaces. Before proposing anything, gather context:
- `/interface/print` — names, which is WAN vs LAN/bridge, link speeds.
- `/ip/address/print` and `/ip/route/print` / `/ip/arp/print` — subnets, gateway, who's behind what.
- Existing config: `/ip/firewall/mangle/print`, `/queue/simple/print`, `/queue/tree/print`,
  `/queue/type/print`. Explain what's already there before changing it.

Confirm the **link capacity** (e.g. WAN = 100 Mbit down / 20 Mbit up) and the goal (cap a user, fair
sharing, prioritize VoIP, …) with the user — QoS numbers are only meaningful against a known ceiling.

## The QoS pipeline (mental model)

1. **Mark** traffic with mangle: first **mark-connection** (cheap, once per connection), then
   **mark-packet** keyed off that connection-mark. Packet marks are what queues match.
2. **Queue** the marked traffic: **simple queues** (top-down, quick per-target limits) *or* a
   **queue tree** (hierarchical HTB, attaches to packet-marks, for real multi-level shaping).
3. Pick the **queue type** that fits (PCQ for per-user fairness, SFQ/RED for fairness/AQM, pfifo
   default). Direction matters: shape **download** on the LAN-egress (or global with dst marks),
   **upload** on the WAN-egress (or global with src marks).

> ⚠️ **FastTrack bypasses queues and mangle.** Connections accepted by a `fasttrack-connection` rule
> skip the queue tree entirely. If you shape traffic, exclude it from FastTrack (or don't FastTrack
> it) or your queues will see almost nothing. Call this out whenever a queue tree "isn't working".

## Step 1 — Mark connections & packets (mangle)

Use `chain=prerouting` for download-side and `chain=postrouting`/`forward` as appropriate; keep
`passthrough=yes` on connection marks and `passthrough=no` on the final packet mark.

Per-user fairness example (mark all forwarded user traffic, split up/down):
- `/ip/firewall/mangle/add` · `=chain=forward` · `=action=mark-connection` · `=new-connection-mark=users` · `=connection-state=new` · `=passthrough=yes`
- Download: `/ip/firewall/mangle/add` · `=chain=forward` · `=connection-mark=users` · `=in-interface-list=WAN` · `=action=mark-packet` · `=new-packet-mark=users-down` · `=passthrough=no`
- Upload: `/ip/firewall/mangle/add` · `=chain=forward` · `=connection-mark=users` · `=out-interface-list=WAN` · `=action=mark-packet` · `=new-packet-mark=users-up` · `=passthrough=no`

(For marking router-originated or to-router traffic use `output` / `input` chains.)

## Step 2 — Choose a queue type

- **PCQ** — per-connection-queue, the workhorse for *fair sharing among many users*. Classify by the
  address that identifies a subscriber: download → `pcq-classifier=dst-address`, upload →
  `src-address`. `pcq-rate=0` = unlimited per-user (just share); set a value to cap each user.
  - `/queue/type/add` · `=name=pcq-down` · `=kind=pcq` · `=pcq-classifier=dst-address` · `=pcq-rate=0`
  - `/queue/type/add` · `=name=pcq-up` · `=kind=pcq` · `=pcq-classifier=src-address` · `=pcq-rate=0`
- **sfq** — stochastic fairness for a single aggregate; **red** — AQM to manage bufferbloat;
  **pfifo/bfifo** — plain FIFO (default). Pick PCQ when "every user gets a fair share"; HTB hierarchy
  (queue tree) when you need nested guarantees/priorities.

## Step 3 — Build the queue tree (HTB hierarchy)

A tree = a **parent** that owns the total bandwidth + **children** that match packet-marks. `parent`
is either an interface name or `global`. Use `max-limit` (ceiling) and `limit-at` (guarantee) and
`priority` (1=highest..8) on children; children of the same parent share/borrow up to the parent's
`max-limit`.

Download tree (shape on the LAN bridge egress), with VoIP prioritized over bulk user traffic:
- Root: `/queue/tree/add` · `=name=down-total` · `=parent=bridge-lan` · `=max-limit=100M`
- VoIP: `/queue/tree/add` · `=name=down-voip` · `=parent=down-total` · `=packet-mark=voip-down` · `=limit-at=10M` · `=max-limit=100M` · `=priority=1`
- Users: `/queue/tree/add` · `=name=down-users` · `=parent=down-total` · `=packet-mark=users-down` · `=queue=pcq-down` · `=max-limit=100M` · `=priority=5`

Upload tree mirrors it on the WAN interface (`=parent=ether1`, `=queue=pcq-up`, upload packet-marks).

> Simple alternative: for one or a few targets, `/queue/simple/add` · `=name=guest` ·
> `=target=192.168.88.0/24` · `=max-limit=20M/20M` is faster than a tree. Simple queues run *before*
> the global queue tree — don't accidentally double-shape.

## Audit / recommendations

- Are marked packets actually reaching the queues (check queue `bytes`/`rate` counters via
  `/queue/tree/print stats`)? If ~zero, suspect **FastTrack** or a wrong chain/interface.
- `max-limit` set on the root to the real link rate? Children's `limit-at` sums ≤ parent `max-limit`?
- Right classifier direction (dst for download, src for upload)? Priorities set where latency matters
  (VoIP/gaming = priority 1)? PCQ where per-user fairness is wanted?
- Mangle: connection-mark passthrough=yes, final packet-mark passthrough=no, marks made once (state=new).

## Safety

These are writes — confirm with the user. Mis-shaping degrades throughput but rarely locks you out;
still prefer reversible steps (add `=disabled=yes`, verify counters, then enable). See the
`mikrotik-admin` skill for the global rules.

## Reference (MikroTik docs — manual.mikrotik.com)

- [Mangle](https://manual.mikrotik.com/docs/firewall-and-quality-of-service/firewall/mangle) ·
  [Common matchers & actions](https://manual.mikrotik.com/docs/firewall-and-quality-of-service/firewall/common-firewall-matchers-and-actions) ·
  [Packet Flow in RouterOS](https://manual.mikrotik.com/docs/firewall-and-quality-of-service/packet-flow-in-routeros)
- [Queues (overview)](https://manual.mikrotik.com/docs/firewall-and-quality-of-service/queues/) ·
  [HTB](https://manual.mikrotik.com/docs/firewall-and-quality-of-service/queues/htb-hierarchical-token-bucket) ·
  [Queue Types](https://manual.mikrotik.com/docs/firewall-and-quality-of-service/queues/queue-types/) ·
  [PCQ example](https://manual.mikrotik.com/docs/firewall-and-quality-of-service/queues/pcq-example) ·
  [Queue Burst](https://manual.mikrotik.com/docs/firewall-and-quality-of-service/queues/queue-burst)
