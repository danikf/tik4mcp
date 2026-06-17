---
name: mikrotik-admin
description: Administer MikroTik routers via the tik4mcp MCP server. Use when the user wants to inspect or change MikroTik/RouterOS router state, discover routers on the network (MNDP), check interfaces/IP/firewall/DHCP/logs, or run RouterOS commands over API/REST/Telnet/MAC-Telnet/WinBox.
---

# MikroTik administration (tik4mcp)

This skill drives MikroTik RouterOS routers through the **tik4mcp** MCP server. Prefer the tik4mcp
tools over shelling out; they handle transports, the router inventory, and safety guardrails.

## How to work

1. **Find the target.** If the user names a router loosely, call `mikrotik_list_routers` to see the
   configured inventory (names, hosts, transport, writability). To find routers on the local network,
   use `mikrotik_discover` (MNDP — works even without an IP route).
2. **Read before you write.** For common questions use the curated read tools first:
   `mikrotik_system_overview`, `mikrotik_interfaces`, `mikrotik_ip_addresses`, `mikrotik_logs`.
3. **Use `mikrotik_command` for everything else.** Pass the RouterOS API path (e.g.
   `/ip/firewall/filter/print`) and API-form parameters: filters as `?name=value`, name-value words
   as `=name=value`. Target by inventory `router` name, or ad-hoc `host`/`username`/`password`.

## Safety rules

- The server is **read-only by default**. Write commands (`add`/`set`/`remove`/`move`/…) are refused
  unless the router has been made writable in config. Do not try to work around this.
- **Before any write**, state plainly what will change and confirm with the user. Never modify
  firewall, addressing, or routing without explicit confirmation — a mistake can lock the admin out.
- Never paste router passwords into chat. Routers are referenced by inventory name; credentials live
  in the server's environment.
- When a write is rejected by policy, explain that the router is read-only rather than retrying.

## Transports

`Api` (default), `ApiSsl`, `Rest`, `RestSsl`, `Telnet`, `MacTelnet`, `WinboxCli`, `WinboxCliMac`,
`WinboxNative`. Use `MacTelnet`/`WinboxCliMac` (with a MAC from `mikrotik_discover`) to reach a router
that has no IP — useful for recovery and bootstrap.
