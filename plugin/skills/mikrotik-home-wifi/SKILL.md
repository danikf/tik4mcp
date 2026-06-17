---
name: mikrotik-home-wifi
description: Build a home Wi-Fi setup on MikroTik via tik4mcp — multiple/virtual WLANs (SSIDs), an isolated guest network, and a kids/child WLAN with parental controls (forced family-safe DNS like OpenDNS FamilyShield or Cloudflare for Families) plus time-based internet curfews via the scheduler. Use when the user wants several SSIDs, a guest network, parental controls, safe DNS, or scheduled (time-based) access on a home router.
---

# MikroTik home Wi-Fi — virtual WLANs, guest, and parental controls

A practical recipe for a home router: separate SSIDs for **main**, **kids**, and **guest**, each on
its own subnet so firewall/DNS policy can target it. Drive everything through tik4mcp
(`mikrotik_command` for reads and all writes). This skill composes the `mikrotik-ip` (segments,
DHCP, DNS) and `mikrotik-firewall` (rules, lockout safety) skills — read those for the underlying
mechanics; here we wire them together.

## Read first

- `mikrotik_interfaces` + `/interface/wifi/print` (RouterOS 7.13+ "wifiwave2") **or**
  `mikrotik_wireless` (`/interface/wireless`, legacy driver) — know which Wi-Fi stack the device has.
- `/interface/bridge/print`, `mikrotik_ip_addresses`, `/ip/dhcp-server/print`, `/ip/dns/print` —
  current segmentation and DNS.

> ⚠️ If you are connected **over Wi-Fi**, reconfiguring the AP can disconnect you. Prefer a wired/API
> connection (or MAC/WinBox) for these changes. Also ensure the clock is correct (NTP) — the curfew
> scheduler runs on router time; see `mikrotik-ip`.

## Segmentation model (each WLAN = its own subnet)

Simplest home approach: **one bridge per network** (no VLAN knowledge needed), each with its own
subnet + DHCP, and grouped into interface lists for firewall matching:

| Network | Bridge | Subnet | Interface list |
|---|---|---|---|
| Main | `bridge-lan` | 192.168.88.0/24 | LAN |
| Kids | `bridge-kids` | 192.168.20.0/24 | KIDS |
| Guest | `bridge-guest` | 192.168.30.0/24 | GUEST |

(Scalable alternative: one VLAN-filtering bridge + a VLAN per SSID — cleaner on managed switches, more
moving parts. Use it if the user already runs VLANs.)

For each new bridge, follow `mikrotik-ip`: `/interface/bridge/add`, `/ip/address/add` (gateway IP),
`/ip/pool/add`, `/ip/dhcp-server/add` + `/ip/dhcp-server/network/add` (set its `gateway` and
`dns-server` to the bridge IP), and add the bridge to its interface list
(`/interface/list/member/add`).

## Create the virtual WLANs (extra SSIDs)

**wifiwave2 (`/interface/wifi`, ROS 7.13+):** add a virtual AP with `master-interface` and put it on
its segment bridge.

- Kids SSID: `/interface/wifi/add` · `=name=wifi-kids` · `=master-interface=wifi1` ·
  `=configuration.mode=ap` · `=configuration.ssid=Home-Kids` ·
  `=security.authentication-types=wpa2-psk,wpa3-psk` · `=security.passphrase=<kids-pass>` · `=disabled=no`
- Guest SSID: same with `=name=wifi-guest` · `=configuration.ssid=Home-Guest` ·
  `=security.passphrase=<guest-pass>` · **`=configuration.client-isolation=yes`** (stops guest↔guest).
- Attach each to its bridge: `/interface/bridge/port/add` · `=bridge=bridge-kids` · `=interface=wifi-kids`
  (and `bridge-guest`/`wifi-guest`).

(Reusable `/interface/wifi/configuration` and `/interface/wifi/security` profiles are cleaner for many
SSIDs; inline dotted props above are fine for a few.)

**Legacy (`/interface/wireless`):** create a security profile then a virtual AP:
- `/interface/wireless/security-profiles/add` · `=name=sp-kids` · `=mode=dynamic-keys` ·
  `=authentication-types=wpa2-psk` · `=wpa2-pre-shared-key=<kids-pass>`
- `/interface/wireless/add` · `=name=wlan-kids` · `=master-interface=wlan1` · `=mode=ap-bridge` ·
  `=ssid=Home-Kids` · `=security-profile=sp-kids` · `=disabled=no` (guest: add `=default-forwarding=no`
  for client isolation). Then add to the bridge as above.

## Parental controls on the kids WLAN

### Forced family-safe DNS (cannot be bypassed by changing the device's DNS)

dst-NAT **all** DNS from the kids subnet to a filtering resolver, so it applies no matter what DNS a
device is configured with. Pick a provider:
- **OpenDNS FamilyShield**: `208.67.222.123`, `208.67.220.123`
- **Cloudflare for Families** (malware + adult): `1.1.1.3`, `1.0.0.3`

Redirect UDP and TCP 53:
- `/ip/firewall/nat/add` · `=chain=dstnat` · `=src-address=192.168.20.0/24` · `=protocol=udp` ·
  `=dst-port=53` · `=action=dst-nat` · `=to-addresses=208.67.222.123` · `=to-ports=53` ·
  `=comment=kids-force-dns`
- Repeat with `=protocol=tcp`.

> **Bypass caveat — say this to the user.** Forced plaintext-DNS redirect does **not** stop DNS-over-
> HTTPS/TLS (DoH/DoT) built into browsers/phones. To tighten: block `tcp dst-port=853` (DoT) from the
> kids subnet, and optionally maintain a drop `address-list` of known DoH endpoints. It's best-effort,
> not absolute — set expectations honestly.

### Time-based internet curfew (scheduler-driven, version-safe)

The old RouterOS 6 firewall `time` matcher is unreliable/removed on v7 — use the **scheduler** to flip
a drop rule instead (works on all versions). Create the rule disabled, then toggle it by `comment`:

1. `/ip/firewall/filter/add` · `=chain=forward` · `=src-address=192.168.20.0/24` ·
   `=out-interface-list=WAN` · `=action=drop` · `=comment=kids-curfew` · `=disabled=yes`
2. Curfew **on** at 21:00: `/system/scheduler/add` · `=name=kids-curfew-on` · `=start-time=21:00:00` ·
   `=interval=1d` · `=on-event=/ip/firewall/filter set [find comment=kids-curfew] disabled=no`
3. Curfew **off** at 07:00: `/system/scheduler/add` · `=name=kids-curfew-off` · `=start-time=07:00:00` ·
   `=interval=1d` · `=on-event=/ip/firewall/filter set [find comment=kids-curfew] disabled=yes`

Place the drop rule **before** any general LAN→WAN accept (order matters — see `mikrotik-firewall`).
For per-day schedules, add separate scheduler entries or check the weekday in an `on-event` script.

## Guest WLAN isolation (firewall)

Guests get internet only — no access to other home networks or the router (except DHCP/DNS):
- Forward: `/ip/firewall/filter/add` · `=chain=forward` · `=in-interface-list=GUEST` ·
  `=out-interface-list=!WAN` · `=action=drop` · `=comment=guest->only-WAN` (drops guest→LAN/KIDS;
  established/related already accepted by the baseline). Ensure guest→WAN accept exists.
- Input (to router): allow only what guests need — `udp 53` (DNS) and `udp 67` (DHCP) from `GUEST`,
  drop the rest of `GUEST` input. Keep these **above** the general input drop.
- AP-level client isolation already set above; optional bandwidth cap via a simple queue on
  192.168.30.0/24 (see `mikrotik-mangle-queue`). Consider forcing safe DNS on guest too.

## Verify & safety

- After changes: re-print rules/NAT/scheduler and confirm order; check a kids device actually resolves
  through the filter (e.g. a blocked test domain) and that curfew toggles at the set times.
- All of this is **writes** — confirm with the user first, prefer reversible steps (add `=disabled=yes`,
  verify, enable), and protect your own management access before adding drops (see `mikrotik-firewall`).

## Reference (MikroTik docs — manual.mikrotik.com)

- [WiFi (wifiwave2)](https://manual.mikrotik.com/docs/Wireless/WiFi/) ·
  [CAPsMAN](https://manual.mikrotik.com/docs/Wireless/capsman/) ·
  [Virtual AP / repeater example](https://manual.mikrotik.com/docs/Wireless/WiFi/configuring-repeater/)
- [Filter](https://manual.mikrotik.com/docs/firewall-and-quality-of-service/firewall/filter) ·
  [NAT](https://manual.mikrotik.com/docs/firewall-and-quality-of-service/firewall/nat) ·
  [Scheduler](https://manual.mikrotik.com/docs/cli-reference/system/scheduler/) ·
  [DNS](https://manual.mikrotik.com/docs/network-management/dns) ·
  [DHCP](https://manual.mikrotik.com/docs/network-management/dhcp)
- Related skills: `mikrotik-ip`, `mikrotik-firewall`, `mikrotik-mangle-queue`, `mikrotik-admin`.
