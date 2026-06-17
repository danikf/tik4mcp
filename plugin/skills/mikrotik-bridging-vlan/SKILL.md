---
name: mikrotik-bridging-vlan
description: Configure MikroTik layer-2 — bridges, bridge VLAN filtering (tagged/untagged, PVID), VLAN interfaces for L3, switch-chip/hardware offloading, and loop protection (RSTP). Use when the user wants to set up VLANs, segment the LAN, add a managed-switch trunk, bridge ports together, or enable hardware offload / spanning tree.
---

# MikroTik bridging, VLANs & switching

Drive everything through tik4mcp (`mikrotik_command` for reads and all writes). This skill is the L2
foundation that `mikrotik-home-wifi` (VLAN-per-SSID) and `mikrotik-ip` (an IP per VLAN) build on.
Prefer the linked docs over memory.

## Bridge basics

A **bridge** is a virtual switch that joins ports into one L2 segment. Modern RouterOS uses **one main
bridge** for the LAN with all local ports added to it:
- `/interface/bridge/add` · `=name=bridge` ; `/interface/bridge/port/add` · `=bridge=bridge` ·
  `=interface=ether2` (repeat per LAN port). Read: `/interface/bridge/print`,
  `/interface/bridge/port/print`.
- Address the **bridge**, not member ports (see `mikrotik-ip`).

## VLANs the modern way — bridge VLAN filtering

For VLANs on one device (and trunks to managed switches/APs), use **bridge VLAN filtering** rather than
per-port bridges. The bridge becomes VLAN-aware; the `/interface/bridge/vlan` table defines which ports
are tagged (trunk) or untagged (access) for each VLAN, and a port's **PVID** tags untagged ingress.

1. Define VLANs in the bridge VLAN table:
   `/interface/bridge/vlan/add` · `=bridge=bridge` · `=vlan-ids=20` · `=tagged=bridge,ether10` ·
   `=untagged=ether2,ether3` (ether10 = trunk to a switch/AP; ether2/3 = access ports; **`bridge`
   must be tagged** for any VLAN the router itself terminates).
2. Set access-port PVIDs: `/interface/bridge/port/set` · `=numbers=<port-id>` · `=pvid=20` (and, for
   strictness, `=frame-types=admit-only-untagged-and-priority-tagged` on access ports;
   `admit-only-vlan-tagged` on trunks). `=ingress-filtering=yes` on all ports.
3. **Enable filtering LAST:** `/interface/bridge/set` · `=numbers=<bridge>` · `=vlan-filtering=yes`.

> ⚠️ **Enabling `vlan-filtering` can lock you out instantly.** Once on, the CPU/management is reachable
> only on a VLAN the bridge is tagged/untagged for. Before flipping it on: make sure your management
> access lands on a configured VLAN (e.g. keep one access port with the right PVID, or tag the mgmt
> VLAN toward the bridge), and prefer doing the switch over a wired/MAC connection. This is the #1
> VLAN lockout. Consider doing it as a reversible batch (see safe mode in `mikrotik-admin`).

## VLAN interfaces for L3 (router-on-a-stick)

To give the router an IP in a VLAN (gateway, DHCP, inter-VLAN routing), add a VLAN interface on the
bridge and treat it like any L3 interface:
- `/interface/vlan/add` · `=name=vlan20` · `=vlan-id=20` · `=interface=bridge`
- Then `/ip/address/add` on `vlan20`, a DHCP server, etc. (see `mikrotik-ip`). Inter-VLAN traffic is
  filtered in the `forward` chain (see `mikrotik-firewall`); drop VLAN↔VLAN where isolation is wanted.

## Switch chip & hardware offloading

- **Bridge hardware offload**: on supported models, bridged ports show an `H` flag and switch at wire
  speed. Keep `/interface/bridge/port` `=hw=yes` (default) so the switch chip does the forwarding;
  bridge VLAN filtering is hardware-offloaded on most modern chips.
- **Switch-chip menu** (`/interface/ethernet/switch`, model-dependent) and **L3 hardware offloading**
  (`l3hw`) push routing/VLAN into the chip. Behaviour is hardware-specific — check the device's specs
  before relying on it.

## Loop protection (STP/RSTP/MSTP)

Bridges run RSTP by default. For redundant links/loops set the mode explicitly and mark edge ports:
- `/interface/bridge/set` · `=numbers=<bridge>` · `=protocol-mode=rstp` (or `mstp`). Watch for loops
  when cabling multiple switches; never bridge two ports that reach the same upstream without STP.

## Read / audit

`/interface/bridge/print`, `/interface/bridge/port/print` (check `H` offload flag),
`/interface/bridge/vlan/print` (the learned + static VLAN table), `/interface/vlan/print`,
`/interface/ethernet/switch/print` (if present). Verify the VLAN table covers the bridge for every
router-terminated VLAN before enabling filtering.

## Safety

These are **writes** that can sever L2 connectivity — confirm with the user, prefer a wired/MAC
management path, and treat `vlan-filtering=yes` and port moves as lockout-class changes (verify the
management path first; do them reversibly). See `mikrotik-admin`.

## Reference (MikroTik docs — manual.mikrotik.com)

- [Bridge (CLI reference — bridge, ports, `/interface/bridge/vlan`, PVID, vlan-filtering)](https://manual.mikrotik.com/docs/cli-reference/interface/bridge/)
- [VXLAN](https://manual.mikrotik.com/docs/Bridging%20and%20Switching/vxlan/) ·
  [macvlan](https://manual.mikrotik.com/docs/cli-reference/interface/macvlan/)
- Related skills: `mikrotik-ip`, `mikrotik-firewall`, `mikrotik-home-wifi`, `mikrotik-capsman`,
  `mikrotik-admin`.
