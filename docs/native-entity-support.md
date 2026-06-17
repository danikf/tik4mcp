# tik4mcp — Native entity support (most-requested object types)

> What "native" means here: a curated, strongly-typed tik4mcp tool surface for a RouterOS object type
> — schema-validated read/write — rather than only the raw `mikrotik_command`. This list prioritizes
> which object types to promote to native tools, grounded in (a) the entities **tik4net.entities
> already ships**, (b) **real demand** from the tik4net issue tracker and MikroTik forum, and
> (c) the **router-from-scratch** provisioning flow that is phase-1's focus.

Legend: **✅** typed entity already exists in `tik4net.entities` (tik4mcp can wrap it cheaply) ·
**⚠️ gap** no typed entity yet (tik4mcp would use raw command or a new entity needs adding upstream).
Tiers: **P1** must-have · **P2** common · **P3** advanced/niche.

## A. Prioritized object types by domain

### System / Management
| Path | tik4net entity | Demand signal | Tier |
|---|---|---|---|
| `/system/identity` | ✅ `SystemIdentity` | First step of every provisioning script | **P1** |
| `/system/resource` | ✅ `SystemResource` | Universal health check | **P1** |
| `/system/routerboard` | ✅ `SystemRouterboard` | Model/firmware info | P2 |
| `/system/clock` | ⚠️ gap | Timezone — needed before certs/scheduler | **P1** |
| `/system/ntp/client` | ⚠️ gap | Time sync for logs/certs | **P1** |
| `/system/backup` + `/export` | ⚠️ gap | Backup-before-change, config diffing | **P1** |
| `/ip/service` | ⚠️ gap | Service hardening (disable telnet/ftp/www) — security baseline | **P1** |
| `/system/scheduler` | ⚠️ gap | Cron automation — frequently scripted | P2 |
| `/system/script` | ⚠️ gap | Stored scripts | P2 |
| `/log` | ✅ `Log` | Diagnostics — already a tik4mcp tool | **P1** |
| `/system/health` | ⚠️ gap | Temp/voltage/fan | P3 |
| `/certificate` | ⚠️ gap | API-SSL/REST-SSL — recurring pain point | P2 |

### Users / Access
| Path | tik4net entity | Demand signal | Tier |
|---|---|---|---|
| `/user` | ✅ `User` | Create agent/admin accounts, lock down default admin | **P1** |
| `/user/group` | ✅ `UserGroup` | RBAC / least-privilege | **P1** |

### Interfaces
| Path | tik4net entity | Demand signal | Tier |
|---|---|---|---|
| `/interface` | ✅ `Interface` | List/enable/disable — universal | **P1** |
| `/interface/ethernet` | ✅ `InterfaceEthernet` | Physical port config | **P1** |
| `/interface/bridge` (+`/port`,`/settings`) | ✅ `InterfaceBridge`/`BridgePort`/`BridgeSettings` | Core L2 topology on RouterOS 7 | **P1** |
| `/interface/vlan` | ✅ `InterfaceVlan` | VLAN segmentation — very common | **P1** |
| `/interface/list` (+`/member`) | ⚠️ gap | WAN/LAN grouping for modern firewall — **requested (issue #85)** | **P1** |
| `/interface/monitor-traffic` | ✅ `InterfaceMonitorTraffic` | Live throughput | P2 |
| `/interface/pppoe-client` | ✅ `InterfacePppoeClient` | Common WAN uplink | P2 |
| `/interface/wifi` (wifiwave2, ROS 7.13+) | ⚠️ gap | New default Wi-Fi stack | **P1** |
| `/interface/wireless` (legacy) | ✅ `InterfaceWireless` (+ security-profiles, reg-table) | Large installed base | **P1** |
| `/interface/bonding`, `/interface/vrrp` | ⚠️ gap | LAG / HA | P3 |

### IP / Addressing
| Path | tik4net entity | Demand signal | Tier |
|---|---|---|---|
| `/ip/address` | ✅ `IpAddress` | Assign IPs — already a tik4mcp tool | **P1** |
| `/ip/route` | ✅ `IpRoute` | Default + static routes | **P1** |
| `/ip/dns` (+ static) | ✅ `IpDns`/`DnsStatic` | Resolver config | **P1** |
| `/ip/pool` | ✅ `IpPool` | Backs DHCP/PPP | **P1** |
| `/ip/dhcp-client` | ✅ `IpDhcpClient` | WAN via DHCP | **P1** |
| `/ip/arp` | ✅ `IpArp` | Static ARP / bindings | P3 |
| `/ipv6/*` (address/route/firewall) | ⚠️ gap | IPv6 deployments growing | P2 |
| `/ip/cloud` | ⚠️ gap | DDNS / cloud name | P3 |

### Firewall
| Path | tik4net entity | Demand signal | Tier |
|---|---|---|---|
| `/ip/firewall/filter` | ✅ `FirewallFilter` | Most-scripted RouterOS area (issues #52, #94) | **P1** |
| `/ip/firewall/nat` | ✅ `FirewallNat` | Masquerade + port-forward | **P1** |
| `/ip/firewall/address-list` | ✅ `FirewallAddressList` | Dynamic block/allow lists | **P1** |
| `/ip/firewall/mangle` | ✅ `FirewallMangle` | QoS marking, policy routing | P2 |
| `/ip/firewall/connection` | ✅ `FirewallConnection` | Live conntrack (read) | P2 |
| `/ip/firewall/raw` | ⚠️ gap | Pre-conntrack drops | P3 |

### DHCP
| Path | tik4net entity | Demand signal | Tier |
|---|---|---|---|
| `/ip/dhcp-server` | ✅ `IpDhcpServer`/`DhcpServerConfig` | DHCP service | **P1** |
| `/ip/dhcp-server/network` | ✅ `DhcpServerNetwork` | Gateway/DNS to clients | **P1** |
| `/ip/dhcp-server/lease` | ✅ `DhcpServerLease` | Static leases, inspection | **P1** |
| `/ip/dhcp-server/option` | ✅ `DhcpServerOption` | Custom options | P3 |

### Routing
| Path | tik4net entity | Demand signal | Tier |
|---|---|---|---|
| `/ip/route` (static) | ✅ `IpRoute` | See Addressing | **P1** |
| `/routing/bgp/*` | ✅ `Bgp*` (connection/peer/instance/network/advertisements) | ISP/edge | P2 |
| `/routing/ospf/*` | ⚠️ gap | Dynamic routing | P3 |
| `/routing/table` + `/routing/rule` | ⚠️ gap | Policy routing / multi-WAN | P2 |

### VPN
| Path | tik4net entity | Demand signal | Tier |
|---|---|---|---|
| `/interface/wireguard` (+ peers) | ✅ `InterfaceWireguard` + `WireguardPeer` | Top current VPN demand (issue #92 / PR) | **P1** |
| `/ppp/secret` | ✅ `PppSecret` | PPPoE/L2TP user store — requested (#73, #84) | P2 |
| `/ppp/profile`, `/ppp/active` | ✅ `PppProfile`/`PppActive` | Remote-access VPN | P2 |
| `/ip/ipsec/*` | ⚠️ gap | Site-to-site | P2 |
| `/interface/l2tp-server`, `/interface/ovpn-server` | ⚠️ gap | Remote access / OpenVPN | P3 |

### Queues / QoS
| Path | tik4net entity | Demand signal | Tier |
|---|---|---|---|
| `/queue/simple` | ✅ `QueueSimple` | Per-client limits — requested (issue #93) | **P1** |
| `/queue/tree` | ✅ `QueueTree` | Hierarchical QoS | P2 |
| `/queue/type` | ✅ `QueueType` | Custom disciplines | P3 |

### Hotspot
| Path | tik4net entity | Demand signal | Tier |
|---|---|---|---|
| `/ip/hotspot/user` (+ profile, active, ip-binding) | ✅ `HotspotUser`/`HotspotUserProfile`/`HotspotActive`/`HotspotIpBinding` | Captive portal automation (issue #55) | P2 |

### Tools / Monitoring
| Path | tik4net entity | Demand signal | Tier |
|---|---|---|---|
| `/tool/ping`, `/tool/traceroute` | ✅ `ToolPing`/`ToolTraceroute` | First-reach diagnostics | **P1** |
| `/tool/torch` | ✅ `ToolTorch` | Live traffic troubleshooting | P2 |
| `/tool/wol` | ✅ `ToolWol` | Wake-on-LAN | P3 |
| `/tool/netwatch` | ⚠️ gap | Up/down monitoring + scripts | P2 |
| MNDP discovery | ✅ `MndpHelper` | Find routers, no IP — already a tik4mcp tool | **P1** |

## B. Phase-1 — "set up a router from scratch"

The ordered provisioning flow and the entity set it needs (most already exist in tik4net.entities;
the gaps marked ⚠️ are what to add or drive via raw command first):

1. **Reach the box (no IP yet)** — MNDP discover ✅ + `WinboxCliMac`/`MacTelnet` ✅ (tik4mcp strength; demand: issue #89 "Connect via MAC Address?").
2. **Identity** — `/system/identity` ✅.
3. **Users / lock down default admin** — `/user` ✅ (+ `/user/group` ✅): add strong admin/agent account, then change/clear the default `admin` and disable it.
4. **WAN uplink** — `/ip/dhcp-client` ✅, or `/ip/address` ✅ (static) / `/interface/pppoe-client` ✅.
5. **LAN bridge + addressing** — `/interface/bridge`(+`/port`) ✅, then `/ip/address` ✅ on the bridge.
6. **Default route** — `/ip/route` ✅ (skip if DHCP/PPPoE supplies it).
7. **DNS** — `/ip/dns` ✅ (servers + `allow-remote-requests`).
8. **NAT masquerade** — `/ip/firewall/nat` ✅, with `/interface/list`(+member) ⚠️ defining WAN/LAN.
9. **Basic firewall** — `/ip/firewall/filter` ✅ baseline; optional `/ip/firewall/address-list` ✅ mgmt allowlist.
10. **DHCP server for LAN** — `/ip/pool` ✅, `/ip/dhcp-server` ✅, `/ip/dhcp-server/network` ✅, optional static `/ip/dhcp-server/lease` ✅.
11. **Time** — `/system/clock` ⚠️ + `/system/ntp/client` ⚠️.
12. **Services hardening** — `/ip/service` ⚠️ (disable telnet/ftp/api/www, restrict by address); `/certificate` ⚠️ for SSL transports.
13. **Wireless (optional)** — `/interface/wifi` ⚠️ (ROS 7.13+) or `/interface/wireless` ✅ (+ security-profile ✅).
14. **Backup baseline** — `/system/backup` ⚠️ + `/export` ⚠️.

**Highest-value gaps to close for phase-1:** `/interface/list`(+member), `/ip/service`,
`/system/clock`, `/system/ntp/client`, `/system/backup`+`/export`. Everything else in the flow already
has a typed entity in `tik4net.entities`.

## C. Common tik4net feature questions / pain points (from the issue tracker)

- **Connection stability** — "Can not read sentence from connection" recurs (issues #61, #82, #83); reboot/shutdown causes `IOException` (#100). tik4mcp should surface clean errors and not leak these.
- **RouterOS 7 support** — explicit requests (#101 "Support routerOS 7?", #103 v7.18 `!empty` response). Field/path drift between ROS 6 and 7 affects typed entities.
- **v6.43+ login** — historical auth confusion (#63, #64); confirm the alpha uses the new scheme (it does).
- **Enum/format parsing** — multi-flag enums and values like `established,related,untracked` threw `FormatException` (#94, #79); firewall `protocol=tcp` issue (#52). Be defensive when echoing entity values.
- **`.id` / find semantics** — "Get id of any object" (#95), ID numbering (#71), find without `.id` (#66). The raw tool already exposes `.id`; semantic tools should too.
- **Missing entities** — interface lists (#85), and (per the entity audit) `/ip/service`, `/system/ntp`, `/interface/wifi`, IPsec, OSPF are not yet typed.
- **Async / listen** — `LoadAsync` with listen/follow (#88); note REST/CLI/WinBox transports don't support Listen/Streaming.

## D. Sources

- tik4net repo & entity classes (audited live via the GitHub API — the ✅ list is exact):
  [github.com/danikf/tik4net](https://github.com/danikf/tik4net), `tik4net.objects/`
- Issue tracker (real demand signals cited above):
  [github.com/danikf/tik4net/issues](https://github.com/danikf/tik4net/issues) ·
  [issues.ecosyste.ms mirror](https://issues.ecosyste.ms/hosts/GitHub/repositories/danikf/tik4net/issues)
- WireGuard support: [tik4net wiki History](https://github.com/danikf/tik4net/wiki/History) (WireguardInterface/WireguardPeer)
- MikroTik forum tik4net thread: [forum.mikrotik.com/viewtopic.php?t=99954](https://forum.mikrotik.com/viewtopic.php?t=99954)
- Usage / CRUD: [tik4net wiki](https://github.com/danikf/tik4net/wiki)

> Note: the ✅/⚠️ split was verified against the live `tik4net.objects` tree on `master`. Before
> building native tools, re-check the alpha branch in case new entities (e.g. `/interface/wifi`) have
> landed since.
