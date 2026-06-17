---
name: router-init
description: Provision a brand-new or factory-reset MikroTik router from scratch via tik4mcp. Use when the user wants to initialize/set up a new MikroTik, do first-time provisioning, configure a factory-default router, connect to a router with no IP (over MAC/WinBox), create the first admin user, or harden the default admin account.
---

# MikroTik router initialization (from scratch)

This skill provisions a **factory-default or factory-reset** MikroTik router end to end using the
tik4mcp MCP server. A fresh router typically has **no IP configuration** and a default **`admin` user
with an empty password**, so the only way in is the **MAC layer** (WinBox over MAC / MAC-Telnet).

## ⚠️ Read this before touching anything

- **Writes must be enabled.** Provisioning creates and changes config. The tik4mcp server is
  read-only by default; the operator must run it with `TIK4MCP_ReadOnly=false`. If writes are
  refused, stop and tell the user to enable write mode — do not try to work around it.
- **Never lock yourself out.** Always follow the order below: create and **verify** the new admin
  account *before* you disable or change the default `admin`. Apply the firewall **after** confirming
  management access. If the user wants a remote (IP) firewall lockdown, add a management allow-rule
  for your own access first.
- **Confirm destructive/securing steps.** Before changing the admin password, disabling admin, or
  applying firewall input drops, state exactly what will happen and get the user's confirmation.
- **Secrets.** Ask the user for the new password out-of-band or let them set it via the server env;
  echo back only that it was set, never the value.

## Step 0 — Discover and select the router

1. Call `mikrotik_discover` (MNDP). It returns each neighbour's `identity`, `version`, `board`,
   `mac`, `ipv4`, and `interfaceName`.
2. If several routers answer, show the list and ask the user which one (match by board/MAC/identity).
   Record the chosen **MAC** — it is the handle for the MAC-layer connection.

## Step 1 — Connect over the MAC layer (no IP needed)

Use `mikrotik_command` ad-hoc with the encrypted WinBox-over-MAC transport, the default credentials,
and the discovered MAC:

- `transport`: `WinboxCliMac` (preferred; encrypted) — or `MacTelnet` as a fallback.
- `username`: `admin`  · `password`: *(empty)*  · `routerMac`: `AA:BB:CC:DD:EE:FF`

Sanity check the connection and whether a default config is present:

- `command`: `/system/identity/print`
- `command`: `/system/resource/print`  → note RouterOS version (affects Wi-Fi: `/interface/wifi` on
  7.13+ vs legacy `/interface/wireless`).
- `command`: `/ip/address/print` and `/user/print` → see what (if any) default config exists.

> If the router has a RouterOS **default configuration** (a bridge, WAN DHCP client, LAN
> 192.168.88.1/24, basic firewall), decide with the user whether to build on it or
> `/system/reset-configuration` to a clean slate first. Treat a reset as destructive — confirm.

## Step 2 — Base provisioning (over the MAC connection)

Run these as writes (`mikrotik_command`, parameters in `=name=value` form). Adapt names/subnets to
the user's intent. A minimal, safe baseline:

1. **Identity** — `/system/identity/set` · `=name=<router-name>`
2. **LAN bridge** — `/interface/bridge/add` · `=name=bridge-lan`; then add LAN ports with
   `/interface/bridge/port/add` · `=bridge=bridge-lan` · `=interface=etherN` (repeat per LAN port).
3. **LAN address** — `/ip/address/add` · `=address=192.168.88.1/24` · `=interface=bridge-lan`
4. **DHCP for LAN** —
   - `/ip/pool/add` · `=name=lan-pool` · `=ranges=192.168.88.10-192.168.88.254`
   - `/ip/dhcp-server/add` · `=name=lan-dhcp` · `=interface=bridge-lan` · `=address-pool=lan-pool` · `=disabled=no`
   - `/ip/dhcp-server/network/add` · `=address=192.168.88.0/24` · `=gateway=192.168.88.1` · `=dns-server=192.168.88.1`
5. **WAN uplink** — typically `/ip/dhcp-client/add` · `=interface=ether1` · `=disabled=no`
   (or a static `/ip/address/add` on the WAN port).
6. **DNS** — `/ip/dns/set` · `=servers=1.1.1.1,8.8.8.8` · `=allow-remote-requests=yes`
7. **NAT** — `/ip/firewall/nat/add` · `=chain=srcnat` · `=action=masquerade` · `=out-interface=ether1`
   (or use an `/interface/list` named WAN and `=out-interface-list=WAN`).
8. **Time** — `/system/clock/set` · `=time-zone-name=<Region/City>`; and enable NTP:
   `/system/ntp/client/set` · `=enabled=yes` · `=servers=pool.ntp.org`

## Step 3 — Create the first real admin user (and verify)

1. **Create** the new account:
   `/user/add` · `=name=<newadmin>` · `=group=full` · `=password=<strong-password>` ·
   `=comment=provisioned by tik4mcp`
2. **Verify it works** — open a *new* `mikrotik_command` call authenticating as `<newadmin>` (same
   transport) and run `/user/print`. **Do not proceed until this succeeds.** If it fails, fix the new
   user before going further — the default admin is still your fallback.

## Step 4 — Switch to a stable IP transport (recommended)

Once the LAN address is up and you can reach the router by IP, move off the MAC layer onto the API
for the rest of setup and ongoing monitoring (faster, supports Listen/streaming):

1. Make sure the API service is enabled: `/ip/service/print` (look at `api` / `api-ssl`); enable plain
   `api` with `/ip/service/set` · `=numbers=api` · `=disabled=no` if needed.
2. **If you intend to use `ApiSsl` (or `RestSsl`), create and assign a certificate FIRST** — the SSL
   service won't present TLS without one. Create a self-signed server cert, sign it, and attach it:
   - `/certificate/add` · `=name=api-cert` · `=common-name=<router-ip-or-name>` · `=days-valid=3650` · `=key-usage=tls-server`
   - `/certificate/sign` · `=numbers=api-cert` (signing runs in the background; check with `/certificate/print`)
   - `/ip/service/set` · `=numbers=api-ssl` · `=certificate=api-cert` · `=disabled=no`

   tik4mcp's SSL transports accept a self-signed router cert by default (`AllowInvalidCertificate`),
   so no client-side trust setup is needed. For a fuller CA setup, see `mikrotik-hardening`.
3. Reconnect ad-hoc as `<newadmin>` with `transport=Api` (or `ApiSsl`) and `host=192.168.88.1`.
4. Suggest the operator add this router to the tik4mcp **inventory** (a named entry, password via
   `TIK4MCP_Routers__<name>__Password`) so future sessions use the name instead of ad-hoc creds.

## Step 5 — Harden the default admin (do this LAST)

Only after Step 3 verified the new account works:

1. Find the default admin's id: `/user/print` (look for `name=admin`, note its `.id`).
2. Either **disable** it — `/user/set` · `=.id=<admin-id>` · `=disabled=yes` —
   or, if the user prefers to keep it, **set a strong password** —
   `/user/set` · `=.id=<admin-id>` · `=password=<strong-password>`.
3. **Service hardening** — disable insecure services and restrict by source where possible:
   `/ip/service/print`, then `/ip/service/set` · `=numbers=telnet` · `=disabled=yes` (repeat for
   `ftp`, `www`, `api` if only using `api-ssl`), and optionally `=address=<mgmt-subnet>`.

## Step 6 — Baseline firewall (confirm first)

Apply a standard input/forward baseline on `/ip/firewall/filter` only after management access is
confirmed and the user agrees. At minimum: accept established/related, drop invalid, accept input
from the LAN/management subnet, drop other WAN input; accept forward established/related + LAN→WAN,
drop the rest. If managing remotely, ensure an accept rule for your own access precedes any drop.

## Step 7 — Snapshot

Capture a provisioning baseline for future diffing: `/export` (and/or `/system/backup/save`
· `=name=post-provision`). Report a concise summary of everything configured.

## Transport quick reference

`WinboxCliMac` / `MacTelnet` → reach a router with **no IP** (provisioning, recovery). `Api` /
`ApiSsl` → stable day-to-day management once IP is up. See the `mikrotik-admin` skill for the full
tool surface and the general safety rules.
