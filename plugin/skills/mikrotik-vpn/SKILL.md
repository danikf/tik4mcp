---
name: mikrotik-vpn
description: Set up and troubleshoot VPNs on MikroTik via tik4mcp — WireGuard (road-warrior and site-to-site), L2TP/IPsec remote access for native OS clients, IPsec site-to-site, and the firewall/routing needed to make them work. Use when the user wants remote access to the home/office network, a tunnel between two sites, WireGuard, IPsec, or L2TP/PPTP/SSTP/OpenVPN.
---

# MikroTik VPNs

Drive everything through tik4mcp (`mikrotik_command` for reads and all writes). This skill carries the
choices and the wiring; for exact fields prefer the MikroTik docs linked below. Builds on
`mikrotik-firewall` (open the right ports, don't lock yourself out), `mikrotik-ip` (addressing/routes,
DNS), and `mikrotik-admin` (safety).

## Read first

- WAN reachability: does the router have a **public IP** or need DDNS? Check `/ip/cloud/print`
  (MikroTik DDNS gives a `*.sn.mynetname.net` name) or the WAN address. A VPN server needs a reachable
  endpoint; behind CGNAT, prefer an outbound/site-initiated tunnel or ZeroTier.
- Current state: `/interface/wireguard/print`, `/ip/ipsec/...`, `/interface/l2tp-server/server/print`,
  `/ip/address/print`, `/ip/firewall/filter/print`.

## Choosing a VPN

- **WireGuard** — modern default: fast, simple, key-based, roams well. Needs a client app and a
  reachable UDP port. Best choice for new road-warrior and site-to-site setups.
- **L2TP/IPsec** — works with **built-in OS clients** (Windows/macOS/iOS/Android), no app to install.
  Good for non-technical users. More firewall ports.
- **IPsec (IKEv2)** — best for **site-to-site** interop with non-MikroTik gear; also native-client
  capable via mode-config. Most complex to configure.
- **SSTP / OpenVPN** — tunnel over TCP 443, useful when only HTTPS gets out of a restrictive network.

## WireGuard — road warrior (remote device → home LAN)

1. Interface (keys auto-generate; read the public key afterwards):
   `/interface/wireguard/add` · `=name=wg0` · `=listen-port=13231`
   then `/interface/wireguard/print` to read `public-key`.
2. Tunnel subnet on the router: `/ip/address/add` · `=address=10.10.0.1/24` · `=interface=wg0`
3. Add the client as a peer (generate the client's keypair on the client):
   `/interface/wireguard/peers/add` · `=interface=wg0` · `=public-key=<client-public-key>` ·
   `=allowed-address=10.10.0.2/32` (one /32 per client).
4. **Firewall** (see `mikrotik-firewall`): accept the listen port on input from WAN —
   `/ip/firewall/filter/add` · `=chain=input` · `=action=accept` · `=protocol=udp` ·
   `=dst-port=13231` · `=in-interface-list=WAN` · `=comment=wireguard` — placed **above** the input
   drop. Allow forward between `wg0` and LAN as desired.
5. Client config (on the device): its private key; `[Peer]` = router public key,
   `Endpoint=<router-public-ip-or-DDNS>:13231`, `AllowedIPs=192.168.88.0/24` (split tunnel: just the
   LAN) or `0.0.0.0/0` (full tunnel — then add `/ip/firewall/nat` masquerade for wg0→WAN),
   `PersistentKeepalive=25` (helps through NAT).

> WireGuard is stateless/key-based — there are no users/passwords. Restrict each peer's
> `allowed-address` to the minimum. The router only "dials in" if a peer has an `endpoint` set;
> a server-side peer (road warrior) usually has no endpoint.

## WireGuard — site-to-site

Mirror the above on both routers: one `wg0` each, a shared tunnel subnet (e.g. `10.10.0.1/30` and
`.2/30`), each adds the other as a peer with `=endpoint-address`/`=endpoint-port` set to the remote
public IP/port and `=allowed-address=<remote-LAN-subnet>` (and `=persistent-keepalive=25`). Then add
`/ip/route` entries for the remote LAN via the tunnel (or rely on allowed-address routing), and
firewall `forward` accepts both ways.

## L2TP/IPsec — remote access for native OS clients

1. Address pool + profile for clients (see `mikrotik-ip`): `/ip/pool/add` · `=name=vpn-pool` ·
   `=ranges=10.20.0.10-10.20.0.100`; `/ppp/profile/add` · `=name=vpn` · `=local-address=10.20.0.1` ·
   `=remote-address=vpn-pool` · `=dns-server=10.20.0.1`.
2. Enable the server with IPsec: `/interface/l2tp-server/server/set` · `=enabled=yes` ·
   `=use-ipsec=yes` · `=ipsec-secret=<strong-psk>` · `=default-profile=vpn`.
3. Users: `/ppp/secret/add` · `=name=alice` · `=password=<strong>` · `=service=l2tp` · `=profile=vpn`
   (this is the "add profile and user" workflow — `/ppp/profile` + `/ppp/secret`).
4. **Firewall** input from WAN, above the drop: accept `udp` `500,4500,1701` and protocol
   `ipsec-esp` — e.g. `/ip/firewall/filter/add` · `=chain=input` · `=action=accept` · `=protocol=udp`
   · `=dst-port=500,4500,1701` · `=in-interface-list=WAN` · `=comment=l2tp/ipsec`. Add forward accepts
   for the VPN pool to reach the LAN.

## IPsec site-to-site (IKEv2) — outline

Configure `/ip/ipsec` objects in order: `profile` (encryption/DH), `peer` (remote address + profile,
`exchange-mode=ike2`), `identity` (auth — PSK or certificate), `proposal` (phase-2 algorithms), and
`policy` (src/dst subnets + `tunnel=yes`). Open `udp 500,4500` + `ipsec-esp` on input, and ensure NAT
rules **don't masquerade** the tunneled traffic (add an accept/`src-nat`-bypass for the remote subnet
before masquerade). Use the IPsec docs — it's the fiddliest VPN to get right.

> **Parsing gotcha:** `/ip/firewall/.../print` and `/ip/ipsec/active-peers`/`remote-peers` can return
> repeated field names (e.g. `local-port`/`remote-port`) that have tripped up tik4net before. If a
> read errors on duplicate fields, narrow it with a `.proplist`/property filter.

## Audit / troubleshooting checklist

- Endpoint reachable? (public IP/DDNS; UDP port open on input **above** the drop; not behind CGNAT.)
- Tunnel up but no traffic? Missing **forward** accepts, missing **routes** to the remote subnet, or
  masquerade NAT is eating tunnel traffic (add a bypass before masquerade).
- WireGuard handshake failing? Wrong public keys, `allowed-address` too narrow, or no
  `persistent-keepalive` through NAT.
- Keys/PSKs strong and unique; each peer/user least-privileged; old/unused peers removed.

## Safety

These are **writes** that expose a service to the internet — confirm with the user, use strong
credentials, and add the firewall accept for the VPN port carefully (above the drop, but don't widen
other exposure). Prefer reversible steps (`=disabled=yes` → verify → enable). See `mikrotik-firewall`.

## Reference (MikroTik docs — manual.mikrotik.com)

- [VPN overview](https://manual.mikrotik.com/docs/virtual-private-networks/) ·
  [WireGuard](https://manual.mikrotik.com/docs/virtual-private-networks/wireguard) ·
  [IPsec](https://manual.mikrotik.com/docs/virtual-private-networks/ipsec/)
- [L2TP](https://manual.mikrotik.com/docs/virtual-private-networks/l2tp/) ·
  [SSTP](https://manual.mikrotik.com/docs/virtual-private-networks/sstp) ·
  [OpenVPN](https://manual.mikrotik.com/docs/virtual-private-networks/openvpn) ·
  [PPTP](https://manual.mikrotik.com/docs/virtual-private-networks/pptp)
- [IP/Cloud (DDNS)](https://manual.mikrotik.com/docs/network-management/cloud/) · related skills:
  `mikrotik-firewall`, `mikrotik-ip`, `mikrotik-admin`.
