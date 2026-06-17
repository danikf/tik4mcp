---
name: mikrotik-ip
description: Understand and configure MikroTik RouterOS IP/L3 basics via tik4mcp — IP addresses, ARP, routes (default/static), DNS client & cache server, and DHCP client & server. Use when the user wants to read/explain or set up addressing, routing, DNS, or DHCP on a MikroTik router.
---

# MikroTik IP / L3 essentials — addresses, ARP, routes, DNS, DHCP

Drive everything through tik4mcp (`mikrotik_command` for reads and all writes — e.g.
`/ip/address/print`, `/ip/arp/print`, `/ip/route/print`). This skill carries the
logic and best practices; for exact field semantics and version specifics, prefer the MikroTik docs
linked below over memory.

## How the pieces fit

`address` (an IP on an interface) → `route` (where to send non-local traffic) → `dns` (name
resolution) → `dhcp-server` (hand addresses/gateway/DNS to LAN clients). On the WAN side a
`dhcp-client` (or PPPoE/static) usually supplies the router's own address, default route, and DNS.

## IP addresses — `/ip/address`

- Assign a LAN gateway IP to the bridge: `/ip/address/add` · `=address=192.168.88.1/24` ·
  `=interface=bridge-lan`. Always include the prefix (`/24`), and address the **bridge**, not member
  ports, on modern RouterOS.
- Read: `/ip/address/print`.

## ARP — `/ip/arp`

- The IP↔MAC table; entries are dynamic by default. For tighter control, set an interface to
  `arp=reply-only` and add **static** ARP entries (`/ip/arp/add` · `=address=` · `=mac-address=` ·
  `=interface=`) — often paired with static DHCP leases to pin a client to one MAC.
- Other interface `arp` modes: `enabled` (default), `proxy-arp`, `local-proxy-arp`, `disabled`.
- Read: `/ip/arp/print`.

## Routes — `/ip/route`

- **Default route**: dst `0.0.0.0/0` via the gateway. Usually auto-added by the WAN dhcp-client/PPPoE
  (`add-default-route=yes`); add a static one with `/ip/route/add` · `=dst-address=0.0.0.0/0` ·
  `=gateway=<next-hop>`.
- `distance` picks the winner among routes to the same dst (lower wins); use it for failover.
  `check-gateway=ping` drops a route when its gateway goes down. Multiple equal-distance gateways = ECMP.
- Read: `/ip/route/print` (look at `dst-address`, `gateway`, `distance`,
  `active`/`dynamic` flags).

## DNS — `/ip/dns` (client *and* cache server)

- Resolver for the router itself: `/ip/dns/set` · `=servers=1.1.1.1,8.8.8.8`. If the WAN
  dhcp-client/PPPoE has `use-peer-dns=yes`, servers are supplied dynamically (shown as `dynamic-servers`).
- Turn the router into a **DNS cache server** for the LAN: `/ip/dns/set` ·
  `=allow-remote-requests=yes` — then hand `192.168.88.1` to clients as their DNS (via DHCP).
  Restrict UDP/TCP 53 from `WAN` in the firewall so the router isn't an open resolver.
- Local name records: `/ip/dns/static/add` · `=name=router.lan` · `=address=192.168.88.1` (also
  regexp/CNAME/FWD types). Read: `/ip/dns/print`, `/ip/dns/static/print`, `/ip/dns/cache/print`.

## DHCP client — `/ip/dhcp-client` (typical WAN uplink)

- `/ip/dhcp-client/add` · `=interface=ether1` · `=disabled=no` (defaults: `add-default-route=yes`,
  `use-peer-dns=yes`). This is the common way the router gets its WAN IP, gateway, and DNS.
- Read/inspect the obtained lease: `/ip/dhcp-client/print`.

## DHCP server — `/ip/dhcp-server` (+ network, pool, lease)

Order to stand up LAN DHCP:
1. Pool: `/ip/pool/add` · `=name=lan-pool` · `=ranges=192.168.88.10-192.168.88.254`
2. Server: `/ip/dhcp-server/add` · `=name=lan-dhcp` · `=interface=bridge-lan` ·
   `=address-pool=lan-pool` · `=lease-time=10m` · `=disabled=no`
3. Network (what clients are told): `/ip/dhcp-server/network/add` · `=address=192.168.88.0/24` ·
   `=gateway=192.168.88.1` · `=dns-server=192.168.88.1`
4. Static lease (pin an IP to a MAC): `/ip/dhcp-server/lease/add` · `=address=192.168.88.50` ·
   `=mac-address=AA:BB:CC:DD:EE:FF` · `=server=lan-dhcp`

Read: `/ip/dhcp-server/print`, `/ip/dhcp-server/network/print`, `/ip/dhcp-server/lease/print` (active
leases show client hostnames and MACs — handy for inventory).

## Safety

These are writes — the server must run with writes enabled, and you should **confirm with the user**
before changing addressing/routing/DNS/DHCP. Changing the LAN address or DHCP can drop clients;
changing the default route or DNS can cut the router's own connectivity. Prefer additive, reversible
steps (add `=disabled=yes`, verify, then enable), or run a connectivity-affecting change as a
**`mikrotik_safe_batch`** transaction so RouterOS auto-reverts it if you lose access. See the
`mikrotik-admin` skill for the global rules.

## Reference (MikroTik docs — manual.mikrotik.com)

- [ARP](https://manual.mikrotik.com/docs/network-management/arp) ·
  [DNS](https://manual.mikrotik.com/docs/network-management/dns) ·
  [DHCP (client & server)](https://manual.mikrotik.com/docs/network-management/dhcp)
- [Routing & networking protocols](https://manual.mikrotik.com/docs/user-guides/routing-and-networking-protocols/) (routes, default route, distance/ECMP)
- [First Time Configuration](https://manual.mikrotik.com/docs/getting-started/first-time-configuration/)
