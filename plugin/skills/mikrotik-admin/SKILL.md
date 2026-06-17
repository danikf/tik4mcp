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
2. **Read before you write.** Two curated read tools cover the common starting points:
   `mikrotik_system_overview` (identity + resource + routerboard in one call — the best "first contact"
   probe) and `mikrotik_logs` (recent log entries). For every other read, use `mikrotik_command` with
   the print path (e.g. `/ip/address/print`, `/ip/route/print`, `/interface/print`,
   `/ip/firewall/filter/print`) — the domain skills list the exact paths.
3. **Use `mikrotik_command` for all other reads and all writes.** Call it with the RouterOS API path
   (e.g. `/ppp/secret/add`) and API-form parameters: filters as `?name=value`, name-value words as
   `=name=value`. Target by inventory `router` name, or ad-hoc `host`/`username`/`password`.
4. **For multi-step, lockout-prone changes, use `mikrotik_safe_batch`.** It runs an ordered list of
   commands as one all-or-nothing Safe Mode transaction (auto-rollback on any failure or dropped
   connection), and `commit=false` gives a real dry-run. See the Safe Mode rule below.

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
- **Safe Mode — use `mikrotik_safe_batch` for lockout-prone, multi-step edits.** RouterOS Safe Mode
  auto-reverts config changes if the controlling session drops, but it only works **within one
  continuous session** — a plain `mikrotik_command` call opens and closes its own connection, so it
  cannot hold Safe Mode across calls. The **`mikrotik_safe_batch`** tool solves this: give it an
  ordered list of steps (`{ command, parameters? }`, same form as `mikrotik_command`); it opens **one**
  session, enters Safe Mode, runs them all, and **commits only if every step succeeds** — any error, or
  a dropped connection mid-batch, makes RouterOS **roll everything back**. Pass `commit=false` for a
  true **dry-run**: all steps are applied and then reverted, proving the router accepts them without
  leaving a change. Prefer it for firewall/addressing/routing changes where a bad rule could strand you.
  It needs a **session transport** (Api/ApiSsl/Telnet/MacTelnet/WinboxCli/WinboxCliMac/WinboxNative —
  not REST), and reverts **configuration only** (not reboots, upgrades, resets, or file ops — keep
  those out of a batch). It still respects the read-only guardrail. **Run one `mikrotik_safe_batch` at a
  time** and don't overlap it with other writes: RouterOS Safe Mode is a single, router-wide slot, so
  concurrent safe-mode sessions interfere with each other's commit/rollback. When a batch isn't appropriate,
  fall back to reversible single steps: add rules with `=disabled=yes`, verify, then enable; change one
  thing at a time; and always keep your own access path open.
- **Protect the router's flash — avoid recurring config writes.** Every config change (including a
  `/system/scheduler` job that flips a rule's `disabled` flag or add/removes objects) is persisted to
  the router's NAND/flash; doing it on a frequent schedule wears the storage and can eventually kill a
  small device. For time-of-day behaviour prefer **static rules with the firewall `time` matcher**
  over scheduler-toggled rules. Reserve the scheduler for genuinely infrequent jobs.

## Transports

`Api` (default), `ApiSsl`, `Rest`, `RestSsl`, `Telnet`, `MacTelnet`, `WinboxCli`, `WinboxCliMac`,
`WinboxNative`. Use `MacTelnet`/`WinboxCliMac` (with a MAC from `mikrotik_discover`) to reach a router
that has no IP — useful for recovery and bootstrap.

Call **`mikrotik_connection_info`** when you need to choose a transport or figure out what to enable on
the router: it lists each transport's features (which transports can run `mikrotik_safe_batch` — i.e.
support Safe Mode), whether an IP is required, and the RouterOS service/port each one needs. Pass a
`router`/`host` and it also reports that router's current `/ip/service` state, so you can see which
transports will actually connect. Remember: an IP transport is unreachable until the router has an IP
and the service is enabled and allowed; MAC transports need neither.
