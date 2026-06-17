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
   `mikrotik_system_overview`, `mikrotik_interfaces`, `mikrotik_ip_addresses`, `mikrotik_routes`,
   `mikrotik_arp`, `mikrotik_queues` (`kind`), `mikrotik_ppp` (`section`), `mikrotik_users`
   (`section`), `mikrotik_hotspot` (`section`), `mikrotik_wireless` (`section`), `mikrotik_logs`.
3. **Use `mikrotik_command` for everything else, and for all writes.** The curated tools above are
   read-only; to create/modify/delete (routes, queues, PPP secrets, users, hotspot, wireless, …) call
   `mikrotik_command` with the RouterOS API path (e.g. `/ppp/secret/add`) and API-form parameters:
   filters as `?name=value`, name-value words as `=name=value`. Target by inventory `router` name, or
   ad-hoc `host`/`username`/`password`.

## Safety rules

- The server is **read-only by default**. Write commands (`add`/`set`/`remove`/`move`/…) are refused
  unless the router has been made writable in config. Do not try to work around this.
- **Before any write**, state plainly what will change and confirm with the user. Never modify
  firewall, addressing, or routing without explicit confirmation — a mistake can lock the admin out.
- **Credentials — prefer the inventory, but be pragmatic.** Referencing a router by an inventory name
  keeps its password in the server's environment rather than the chat, which is the cleaner pattern.
  For a quick home setup, ad-hoc `host`/`username`/`password` is perfectly fine if the user prefers the
  convenience — don't lecture or block them over it. **After initial setup, recommend** creating a
  dedicated, least-privilege RouterOS user for the AI and saving it as a named inventory entry
  (password via `TIK4MCP_Routers__<name>__Password`); see `router-init` and `mikrotik-hardening`.
- When a write is rejected by policy, explain that the router is read-only rather than retrying.
- **Safe Mode (today's limitation).** RouterOS Safe Mode auto-reverts config changes if the
  controlling session drops — but it only works **within one continuous session**. Because each
  `mikrotik_command` call currently opens and closes its own connection, Safe Mode cannot span multiple
  tool calls, so don't promise it for multi-step edits. Until a single-session batch tool exists (see
  the development plan), get the same protection by working **reversibly**: add rules with
  `=disabled=yes`, verify, then enable; change one thing at a time; and always keep your own access
  path open.
- **Protect the router's flash — avoid recurring config writes.** Every config change (including a
  `/system/scheduler` job that flips a rule's `disabled` flag or add/removes objects) is persisted to
  the router's NAND/flash; doing it on a frequent schedule wears the storage and can eventually kill a
  small device. For time-of-day behaviour prefer **static rules with the firewall `time` matcher**
  over scheduler-toggled rules. Reserve the scheduler for genuinely infrequent jobs.

## Transports

`Api` (default), `ApiSsl`, `Rest`, `RestSsl`, `Telnet`, `MacTelnet`, `WinboxCli`, `WinboxCliMac`,
`WinboxNative`. Use `MacTelnet`/`WinboxCliMac` (with a MAC from `mikrotik_discover`) to reach a router
that has no IP — useful for recovery and bootstrap.
