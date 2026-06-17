---
name: mikrotik-monitoring
description: Monitor MikroTik router health and traffic via tik4mcp — resource/health, logging (and remote syslog), Netwatch up/down alerts, live and historical traffic (monitor-traffic, Torch, Graphing), and integration with external monitoring (SNMP, Traffic Flow / NetFlow). Use when the user wants to check router health, watch traffic, set up logging or alerts, diagnose a problem, or hook the router into a monitoring stack.
---

# MikroTik monitoring & observability

Drive everything through tik4mcp. Most monitoring is **reads** (safe); setting up logging/Netwatch/SNMP
are writes (confirm first). Composes `mikrotik-admin` (safety), `mikrotik-firewall` (drop logging), and
`mikrotik-mangle-queue` (queue counters).

## Quick health snapshot (read-only)

- `mikrotik_system_overview` (or `/system/resource/print`) — CPU load, free memory, uptime, RouterOS
  version, architecture. `/system/health/print` — temperature/voltage/fan (hardware-dependent).
- `mikrotik_interfaces` + `/interface/print stats` — running state, rx/tx, **errors/drops** (rising
  errors = cabling/duplex/SFP issues).
- `mikrotik_logs` (`/log/print`) — recent events. `/ip/dhcp-server/lease/print` — who's on the network.

## Reading the log (hint)

The log is the first place to look when diagnosing. Use `mikrotik_logs` (it returns the most recent
entries; pass a `limit`) or `/log/print`. Tips:
- **Filter by topic** instead of scrolling everything: `/log/print` with a query, e.g.
  `?topics=firewall`, `?topics=dhcp`, `?topics=wireless`, `?topics=error`. Combine topics to narrow.
- Entries are **oldest→newest**; the newest are at the bottom — read the tail. The default memory log
  is small and **cleared on reboot**, so set up retention (below) before you need history.
- Correlate by the `time` column; for a specific subsystem also check its own state
  (e.g. `/ip/dhcp-server/lease`, `/interface/wireless/registration-table`) alongside the log.

## Logging — keep useful history

RouterOS logs to a small memory buffer by default (lost on reboot). For retention, add a **remote
syslog** action and route the topics you care about:
- `/system/logging/action/add` · `=name=remote` · `=target=remote` · `=remote=192.168.88.10` (syslog server).
- `/system/logging/add` · `=topics=error,warning,critical` · `=action=remote` (and e.g. `=topics=firewall`
  if you log specific drops). Read config: `/system/logging/print`, `/system/logging/action/print`.

To log specific firewall events, add `action=log` (with `log-prefix=`) rules in the filter — see
`mikrotik-firewall`. Don't log high-volume accept rules (noise + CPU).

## Netwatch — up/down monitoring & alerts

`/tool/netwatch` probes hosts and runs `on-up`/`on-down`. Great for WAN/gateway/server reachability:
- `/tool/netwatch/add` · `=host=1.1.1.1` · `=interval=10s` · `=down-script=...` · `=up-script=...`

> ⚠️ Keep Netwatch actions to **notifications/logging**, not recurring **config rewrites**. Scripts
> that flip rules or add/remove objects on every flap write to flash and wear it (see the flash-wear
> rule in `mikrotik-admin`). For alerts use `/log` or e-mail (`/tool/e-mail`), or send to external
> monitoring. A failover that rewrites routes occasionally is fine; per-second toggling is not.

## Live & historical traffic

- **Live**: `/interface/monitor-traffic` (per-interface bps), `/tool/torch` (top talkers by
  address/port/protocol — great for "what's using the link"), `/queue/.../print stats` for shaped
  traffic (see `mikrotik-mangle-queue`).
- **On-box graphs**: `/tool/graphing` records interface/resource/queue graphs viewable at
  `http://<router>/graphs/`.
- **External (recommended for real history/alerting):**
  - **SNMP** — `/snmp/set =enabled=yes =contact= =location=`, add a v3 community/user, restrict by
    address + firewall. Poll from LibreNMS/Zabbix/PRTG/Grafana. Preferred for long-term metrics.
  - **Traffic Flow** (NetFlow/IPFIX) — `/ip/traffic-flow` exports per-flow data to a collector for
    bandwidth/usage accounting.

Push heavy/long-term monitoring to an external stack (SNMP/Traffic Flow/syslog) rather than on-box
scripts — it's lighter on the router and avoids flash wear.

## What to watch (and typical thresholds)

- **CPU** sustained > ~80% (often a runaway rule, no FastTrack, or DoS) · **free memory** trending down
  · **disk/NAND** low.
- **Interface** errors/drops climbing · link **flaps** (log) · **temperature** high.
- **WAN reachability** (Netwatch) · **DHCP pool** near exhaustion · **firewall drop** spikes (logged) ·
  **conntrack** table near max.

## Safety

Reads are safe. Enabling logging/Netwatch/SNMP/Traffic-Flow are writes — confirm, restrict SNMP and
syslog by address + firewall, and avoid Netwatch/scheduler actions that rewrite config frequently.

## Reference (MikroTik docs — manual.mikrotik.com)

- [Resource](https://manual.mikrotik.com/docs/diagnostics-monitoring-and-troubleshooting/resource) ·
  [Health](https://manual.mikrotik.com/docs/diagnostics-monitoring-and-troubleshooting/health) ·
  [Log](https://manual.mikrotik.com/docs/diagnostics-monitoring-and-troubleshooting/log/) ·
  [Interface stats & monitor-traffic](https://manual.mikrotik.com/docs/diagnostics-monitoring-and-troubleshooting/interface-stats-and-monitor-traffic)
- [Netwatch](https://manual.mikrotik.com/docs/diagnostics-monitoring-and-troubleshooting/netwatch) ·
  [Torch](https://manual.mikrotik.com/docs/diagnostics-monitoring-and-troubleshooting/torch) ·
  [Graphing](https://manual.mikrotik.com/docs/diagnostics-monitoring-and-troubleshooting/graphing)
- [SNMP](https://manual.mikrotik.com/docs/diagnostics-monitoring-and-troubleshooting/snmp) ·
  [Traffic Flow](https://manual.mikrotik.com/docs/diagnostics-monitoring-and-troubleshooting/traffic-flow/) ·
  [Watchdog](https://manual.mikrotik.com/docs/diagnostics-monitoring-and-troubleshooting/watchdog)
- Related skills: `mikrotik-admin`, `mikrotik-firewall`, `mikrotik-mangle-queue`.
