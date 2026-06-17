---
name: mikrotik-capsman
description: Set up centralized multi-AP Wi-Fi on MikroTik with CAPsMAN via tik4mcp — a controller (manager) that provisions and manages many access points (CAPs) with shared SSIDs, security, and per-SSID VLAN datapath. Covers both the wifiwave2 CAPsMAN (/interface/wifi) and legacy CAPsMAN (/caps-man). Use when the user has multiple APs / wants roaming / one config pushed to all access points.
---

# MikroTik CAPsMAN (centralized multi-AP)

CAPsMAN lets one device (the **manager**) push Wi-Fi config to many **CAPs** (access points), so SSIDs,
security, and channels are managed centrally and clients roam between APs. Drive through tik4mcp
(`mikrotik_command`). Builds on `mikrotik-bridging-vlan` (datapath bridge/VLAN), `mikrotik-home-wifi`
(SSID/security choices), and `mikrotik-admin` (safety).

## Pick the right generation — it must match the AP hardware

- **wifiwave2 CAPsMAN** (`/interface/wifi`, RouterOS 7.13+): for `wifi`/Wi-Fi-5/6 (`ax`) hardware.
- **Legacy CAPsMAN** (`/caps-man`): for older `/interface/wireless` (802.11 a/b/g/n/ac) hardware.

A manager of one generation only manages CAPs of the same generation. Check the AP with
`/interface/wifi/print` (wave2) vs `/interface/wireless/print` (legacy). Don't mix generations.

## wifiwave2 CAPsMAN (`/interface/wifi`)

On the **manager** (often the main router):
1. Enable the manager: `/interface/wifi/capsman/set` · `=enabled=yes` · `=interfaces=all` ·
   `=package-path=` · `=upgrade-policy=none` (a self-signed cert is generated for CAP↔manager TLS).
2. Define a reusable configuration (SSID + security + datapath to a bridge/VLAN):
   `/interface/wifi/configuration/add` · `=name=home` · `=ssid=Home` ·
   `=security.authentication-types=wpa2-psk,wpa3-psk` · `=security.passphrase=<pass>` ·
   `=datapath.bridge=bridge` (add `=datapath.vlan-id=20` to drop this SSID onto a VLAN — see
   `mikrotik-bridging-vlan`).
3. Auto-provision connecting CAPs onto that configuration:
   `/interface/wifi/provisioning/add` · `=action=create-dynamic-enabled` · `=supported-bands=...` ·
   `=master-configuration=home` (radios bind by identity/radio-mac and get the config).

On each **CAP** (access point):
- `/interface/wifi/cap/set` · `=enabled=yes` · `=discovery-interfaces=<lan-iface>` ·
  `=caps-man-addresses=<manager-ip>` · `=slaves-datapath=bridge`. The CAP's radios then appear on the
  manager and get provisioned.

## Legacy CAPsMAN (`/caps-man`)

On the **manager**:
1. `/caps-man/manager/set` · `=enabled=yes`.
2. `/caps-man/configuration/add` · `=name=home` · `=ssid=Home` · `=security.authentication-types=wpa2-psk`
   · `=security.passphrase=<pass>` · `=datapath.bridge=bridge` (+ `=datapath.vlan-id=20` for a VLAN SSID).
3. `/caps-man/provisioning/add` · `=action=create-dynamic-enabled` · `=master-configuration=home`.

On each **CAP**: `/interface/wireless/cap/set` · `=enabled=yes` · `=discovery-interfaces=<iface>` ·
`=caps-man-addresses=<manager-ip>` · `=bridge=bridge`.

## Datapath / forwarding mode (where client traffic goes)

- **Local forwarding** (recommended for most, esp. with VLANs): the CAP bridges client traffic locally
  to its own bridge/VLAN — scales well, keeps traffic off the manager. Set the datapath bridge on the
  CAP and tag the SSID's VLAN at the edge.
- **Manager (central) forwarding**: all client traffic tunnels to the manager — needed only when the
  manager must see/forward every client frame (e.g. central filtering). Heavier on the manager.

Pair CAPsMAN with `mikrotik-bridging-vlan`: one configuration per SSID, each with its own
`datapath.vlan-id`, so guest/kids/main land on separate VLANs across every AP.

## Read / verify

Manager: `/interface/wifi/capsman/remote-cap/print` or `/caps-man/remote-cap/print` (which CAPs are
connected), `/interface/wifi/registration-table/print` or `/caps-man/registration-table/print`
(connected clients across all APs), `/interface/wifi/provisioning/print`, `/interface/wifi/configuration/print`.

## Safety

These are **writes**; enabling CAPsMAN/CAP takes over the AP's radios and briefly disconnects wireless
clients. Configure over a **wired/MAC** management path (not the Wi-Fi you're about to reprovision),
confirm with the user, and verify CAPs reconnect and clients associate afterward. See `mikrotik-admin`.

## Reference (MikroTik docs — manual.mikrotik.com)

- [WiFi / wifiwave2 CAPsMAN](https://manual.mikrotik.com/docs/wireless/wifi/capsman/) ·
  [WiFi (wifiwave2) overview](https://manual.mikrotik.com/docs/wireless/wifi/) ·
  [Legacy CAPsMAN (`/caps-man`)](https://manual.mikrotik.com/docs/wireless/abgn/capsman/)
- Related skills: `mikrotik-bridging-vlan`, `mikrotik-home-wifi`, `mikrotik-ip`, `mikrotik-admin`.
