---
name: mikrotik-hardening
description: Secure and maintain a MikroTik router via tik4mcp — harden access (services, users & rights, neighbor/MAC discovery, management exposure), back up and export config safely, and upgrade RouterOS/firmware. Use when the user wants to lock down/secure a router, audit its exposure, disable unused services, restrict management, take a backup before changes, or upgrade.
---

# MikroTik hardening & maintenance

Drive everything through tik4mcp (`mikrotik_command` for reads and all writes). This skill is the
security/maintenance baseline; it composes `mikrotik-firewall` (input lockdown), `mikrotik-ip`
(DNS/services), and `mikrotik-admin` (safety). Prefer the linked docs over memory.

## Read first

`/ip/service/print`, `/user/print` + `/user/group/print` (or `mikrotik_users`),
`/ip/neighbor/discovery-settings/print`, `/tool/mac-server/print`, `/system/package/print`,
`/system/routerboard/print`. Summarize current exposure before changing anything.

## Harden management access

1. **Services** (`/ip/service`) — disable what you don't use and restrict the rest to the management
   subnet. Prefer encrypted (`api-ssl`, `winbox`) over plaintext (`telnet`, `ftp`, `api`, `www`):
   - `/ip/service/set` · `=numbers=telnet` · `=disabled=yes` (repeat: `ftp`, `api`, `www`).
   - `/ip/service/set` · `=numbers=winbox` · `=address=192.168.88.0/24` (lock to mgmt subnet; repeat
     for `api-ssl`/`ssh`). Optionally change default ports.
2. **Users & rights** (`/user`, `/user/group`) — one strong, named account per admin; least-privilege
   groups; restrict source: `/user/set` · `=numbers=<id>` · `=address=192.168.88.0/24`. Create the new
   admin and **verify it** before disabling the default `admin` (`/user/set =numbers=admin =disabled=yes`)
   — see `router-init` for the lockout-safe order.
3. **Neighbor discovery & MAC access** — don't advertise or accept management on the WAN:
   - `/ip/neighbor/discovery-settings/set` · `=discover-interface-list=LAN` (MNDP off the WAN).
   - `/tool/mac-server/set` · `=allowed-interface-list=LAN`; `/tool/mac-server/mac-winbox/set` ·
     `=allowed-interface-list=LAN`; consider `/tool/mac-server/ping/set` · `=enabled=no`.
4. **Disable unused extras** — `/tool/bandwidth-server/set =enabled=no`, `/ip/proxy`, `/ip/socks`,
   `/ip/upnp`, `/ip/cloud` DDNS if not needed. Turn off Wi-Fi WPS/PIN.
5. **Firewall** — the real gate: drop WAN→router input except what's needed; see `mikrotik-firewall`.
   Service `address=` limits and the firewall are belt-and-suspenders — do both.
6. **Certificates** — for `api-ssl`/`sstp`/`webfig` over TLS, create/import a cert with `/certificate`
   and attach it to the service. Note the menus that hold secrets (keys/passwords) when exporting.

## Back up before you change (and for disaster recovery)

Always snapshot **both** formats before risky changes:
- **Binary backup** (full, restores to the same device, can be encrypted):
  `/system/backup/save` · `=name=pre-change` (· `=password=<pw>` to encrypt). Restore via
  `/system/backup/load`.
- **Text export** (human-readable, portable, diff-able): `/export` · `=file=pre-change`. In RouterOS 7
  sensitive values are hidden by default; only add `show-sensitive` if you truly need secrets in the
  file (and treat it as sensitive). Use `/export` of a sub-tree (e.g. `/ip/firewall/export`) for
  focused diffs.

Pair this with the planned backup-before-change/rollback workflow.

## Upgrade RouterOS & firmware

1. **Back up first** (above). Check current: `/system/package/update/check-for-updates`.
2. **RouterOS packages**: `/system/package/update/download` then reboot, or
   `/system/package/update/install`. Stay on the **stable** channel for production; read release notes.
3. **RouterBOOT firmware** (after the OS upgrade): `/system/routerboard/print` (compare
   `current-firmware` vs `upgrade-firmware`), then `/system/routerboard/upgrade` and **reboot**.
4. Upgrades **disconnect** the device and may take minutes — confirm timing with the user, and have
   out-of-band (MAC/WinBox) access in mind in case it doesn't come back. Netinstall is the recovery
   path of last resort.

## Reset (destructive — confirm explicitly)

`/system/reset-configuration` wipes config; useful flags: `=no-defaults=yes` (clean slate, no defconf),
`=keep-users=yes` (keep accounts), `=run-after-reset=<script>`. Treat as destructive: confirm, ensure
a backup/export exists, and plan MAC-layer re-access (see `router-init`).

## Hardening audit checklist

- Plaintext services (telnet/ftp/api/www) disabled? Remaining services restricted by `address` and/or
  firewall, not reachable from WAN?
- Default `admin` disabled/renamed with a strong password? Each admin least-privileged and source-limited?
- MNDP/MAC-server limited to LAN? Bandwidth-server/UPnP/proxy/SOCKS off if unused?
- DNS not an open resolver (no `allow-remote-requests` reachable from WAN)? Strong Wi-Fi (WPA2/3, no WPS)?
- A recent backup **and** export exist? RouterOS on a current stable; RouterBOOT firmware matched?

## Safety

All of this is **writes**, several of which can lock you out (services, firewall, users) — confirm with
the user, change one thing at a time, keep your own access path, and back up first. See `mikrotik-admin`.

## Reference (MikroTik docs — manual.mikrotik.com)

- [First Time Configuration](https://manual.mikrotik.com/docs/getting-started/first-time-configuration/) ·
  [Default configurations](https://manual.mikrotik.com/docs/getting-started/configuration-management/default-configurations) ·
  [Menus with sensitive parameters](https://manual.mikrotik.com/docs/getting-started/configuration-management/list-of-menus-with-sensitive-parameters)
- [MAC server](https://manual.mikrotik.com/docs/management-tools/mac-server) ·
  [SSH](https://manual.mikrotik.com/docs/management-tools/ssh) ·
  [RoMON](https://manual.mikrotik.com/docs/management-tools/romon)
- [Backup](https://manual.mikrotik.com/docs/getting-started/configuration-management/backup) ·
  [Configuration reset](https://manual.mikrotik.com/docs/getting-started/configuration-management/routeros-configuration-reset) ·
  [Upgrade](https://manual.mikrotik.com/docs/getting-started/installation-and-upgrade/upgrade/) ·
  [Netinstall](https://manual.mikrotik.com/docs/getting-started/installation-and-upgrade/netinstall/)
- Related skills: `mikrotik-firewall`, `mikrotik-ip`, `router-init`, `mikrotik-admin`.
