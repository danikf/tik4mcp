# tik4mcp

**An MCP (Model Context Protocol) server that lets AI assistants administer MikroTik routers.**

tik4mcp exposes MikroTik RouterOS routers to AI agents (Claude Desktop, Claude Code, and any other
MCP client) through a small set of safe, auditable tools. It is built on
[tik4net](https://github.com/danikf/tik4net) and inherits **every** tik4net transport — so an agent
can reach a router over the API, REST, Telnet, MAC-Telnet, or the encrypted WinBox channel, including
over the MAC layer with **no IP route** at all.

> ⚠️ **Alpha.** Interfaces and configuration may change. Read-only by default — see *Safety* below.

## Why tik4mcp

The tik4net repo ships a small developer/debug MCP tool (`Tools/tik4net.mcp`) with a single
`mikrotik_call` for poking the protocol. **tik4mcp is the admin-grade product**: a router inventory,
read-only-by-default guardrails, an audit log, MNDP discovery, and curated semantic tools — designed
for connecting AI agents to real routers for ops, diagnostics, and automation.

## Features

- **All tik4net transports** — `Api`, `ApiSsl`, `Rest`, `RestSsl`, `Telnet`, `MacTelnet`,
  `WinboxCli`, `WinboxCliMac`, `WinboxNative`. (SSH lives in the optional `tik4net.ssh` package and is
  on the roadmap.)
- **Router inventory** — refer to routers by logical name (`core`, `edge-1`); credentials come from
  environment variables / a secret store, never from the AI prompt. Ad-hoc one-off targets are also
  supported.
- **Read & modify router state** — a guarded raw command tool plus curated read tools for the common
  questions (system health, interfaces, IP addresses, logs).
- **MNDP discovery** — find routers on the local segment (identity, version, board, MAC, IP) with no
  IP connectivity required.
- **Safety first** — read-only by default; write commands (`add`/`set`/`remove`/`move`/…) are refused
  unless the router is explicitly made writable. Every command is audit-logged to stderr.

## Tools

| Tool | Purpose |
|---|---|
| `mikrotik_list_routers` | List configured routers (no secrets) and server policy. |
| `mikrotik_discover` | MNDP discovery of routers on the local network. |
| `mikrotik_system_overview` | Identity + resource (CPU/mem/uptime) + routerboard. |
| `mikrotik_interfaces` | Interface list with state and counters. |
| `mikrotik_ip_addresses` | Configured IPv4 addresses. |
| `mikrotik_routes` | IPv4 routing table. |
| `mikrotik_arp` | IPv4 ARP table. |
| `mikrotik_queues` | Traffic queues — `kind`: simple / tree / type. |
| `mikrotik_ppp` | PPP (PPPoE/VPN) — `section`: secret / profile / active. |
| `mikrotik_users` | Login accounts & rights — `section`: users / groups. |
| `mikrotik_hotspot` | Hotspot — `section`: users / profiles / active / bindings. |
| `mikrotik_wireless` | Legacy WLAN — `section`: interfaces / security / registration. |
| `mikrotik_logs` | Most recent log entries. |
| `mikrotik_command` | Run any RouterOS API command over any transport (guarded; the path for all writes). |

## Quick start

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) or newer.

```bash
git clone https://github.com/danikf/tik4mcp.git
cd tik4mcp
dotnet build
```

Run it as a stdio MCP server:

```bash
dotnet run --project src/Tik4Mcp.Server
```

### Configure routers

Edit [`src/Tik4Mcp.Server/appsettings.json`](src/Tik4Mcp.Server/appsettings.json) for non-secret
inventory, and supply passwords (and writability) via `TIK4MCP_`-prefixed environment variables:

```bash
# Allow writes to the "core" router and provide its password
export TIK4MCP_ReadOnly=false
export TIK4MCP_Routers__core__ReadOnly=false
export TIK4MCP_Routers__core__Password='s3cret'
```

### Use from an MCP client

Register tik4mcp as a stdio server. For Claude Desktop (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "tik4mcp": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/tik4mcp/src/Tik4Mcp.Server"],
      "env": {
        "TIK4MCP_Routers__core__Password": "s3cret"
      }
    }
  }
}
```

A Claude Code **plugin bundle** is provided under [`plugin/`](plugin/) — the MCP config plus a set of
knowledge skills that carry RouterOS best-practices and link to the MikroTik docs
(`manual.mikrotik.com`), driving the router through the guarded `mikrotik_command`:

- **`mikrotik-admin`** — general administration, transports, inventory, and the global safety rules.
- **`router-init`** — provision a brand-new / factory-reset router from scratch over the MAC layer
  (discover, connect, set up basics, create the first admin, then lock down the default `admin`).
- **`mikrotik-firewall`** — understand, audit, and safely change the firewall (filter/NAT/mangle/RAW,
  connection state, recommended baseline ruleset, lockout-safe edits).
- **`mikrotik-ip`** — IP addresses, ARP, routes, DNS client & cache server, DHCP client & server.
- **`mikrotik-mangle-queue`** — traffic shaping/QoS: connection & packet marking, hierarchical queue
  trees, and the right queue type (PCQ/HTB).
- **`mikrotik-home-wifi`** — home Wi-Fi: virtual WLANs (multiple SSIDs), an isolated guest network,
  and a kids WLAN with forced family-safe DNS and a `time`-matcher internet curfew (no flash wear).
- **`mikrotik-vpn`** — WireGuard (road-warrior & site-to-site), L2TP/IPsec for native OS clients,
  IPsec site-to-site, and the firewall/routing to make them work.
- **`mikrotik-hardening`** — secure & maintain: services/users/discovery lockdown, backup & export,
  RouterOS/firmware upgrade, reset.
- **`mikrotik-monitoring`** — health/resource, logging & remote syslog, Netwatch alerts, live/historical
  traffic, and SNMP / Traffic Flow integration.
- **`mikrotik-bridging-vlan`** — bridges, bridge VLAN filtering (tagged/untagged, PVID), VLAN
  interfaces for L3, hardware offload/switch chip, and loop protection (RSTP).
- **`mikrotik-capsman`** — centralized multi-AP Wi-Fi (CAPsMAN), both wifiwave2 (`/interface/wifi`) and
  legacy (`/caps-man`): manager, provisioning, shared SSIDs, and per-SSID VLAN datapath.

## Safety

tik4mcp is **read-only by default**. A router becomes writable only when *both* the global
`Tik4Mcp:ReadOnly` flag *and* that router's `ReadOnly` flag are `false`. Every command — read or write
— is logged to stderr with the target router, transport, and write flag. Use a dedicated
least-privilege RouterOS account for the agent.

## Roadmap

- [`docs/development-plan.md`](docs/development-plan.md) — full milestone plan (remote HTTP/SSE
  transport, approval/dry-run workflow, MCP resources & prompts, more semantic tools, SSH).
- [`docs/native-entity-support.md`](docs/native-entity-support.md) — prioritized, demand-grounded
  list of RouterOS object types to support natively, including which already exist in
  `tik4net.entities` and which are gaps.

**Phase 1** focuses on provisioning a router from scratch (the `router-init` skill); see the entity
doc for the minimal entity set it needs.

## License

[MIT](LICENSE) © Daniel Frantik. Built on [tik4net](https://github.com/danikf/tik4net) (Apache 2.0).
