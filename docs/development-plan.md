# tik4mcp — Development plan

> Status: **v1 scaffold in place.** This plan turns the high-level admin-server sketch from the
> tik4net `_notes` into concrete milestones. It records the decisions already taken and the work
> remaining.

## Goal

A production-oriented MCP server that lets MikroTik administrators safely connect AI agents to their
routers for operations, diagnostics, and automation — built on tik4net so it speaks every transport
the library supports.

## Decisions (locked for v1)

| Topic | Decision |
|---|---|
| tik4net dependency | NuGet `tik4net` **4.0.0-alpha** (PackageReference). Tweaks to tik4net flow through new alpha publishes. |
| MCP transport | **stdio now**, architected so HTTP/SSE can be added later. |
| Tool surface | **Raw + curated semantic mix** — guarded `mikrotik_command` plus curated read tools. |
| Inventory & secrets | **Named inventory + ad-hoc.** Inventory in `appsettings.json`; credentials via `TIK4MCP_` env vars / secret store. |
| License | MIT. |
| Runtime | .NET 8, packaged as a dotnet tool (`tik4mcp`) and a Claude Code plugin bundle. |

## Architecture

```
MCP client (Claude) ──stdio──> Tik4Mcp.Server
                                  ├─ Program.cs          host + config (appsettings + TIK4MCP_ env) + DI
                                  ├─ Configuration/      Tik4McpOptions, RouterProfile (inventory model)
                                  ├─ Connections/        ConnectionResolver  → opens tik4net ITikConnection per transport
                                  ├─ Security/           AccessPolicy        → read/write classification + read-only guardrail
                                  └─ Tools/              MCP tools (raw command, system, discovery, inventory)
                                          └────────────> tik4net 4.0.0-alpha (all transports + MNDP)
```

Every write passes through `AccessPolicy.EnsureAllowed`. Every connection is built by
`ConnectionResolver`, the single place that knows about transports and the inventory.

## Milestones

### M0 — Scaffold ✅ (done)
- Solution, server project, tik4net alpha reference, MIT license, README, dev plan.
- DI host with stdio transport; configuration binding (appsettings + env override).
- `ConnectionResolver` covering all in-core transports; `AccessPolicy` read-only guardrail.
- Tools (14): inventory `mikrotik_list_routers`; discovery `mikrotik_discover`; curated read tools
  `mikrotik_system_overview`, `mikrotik_interfaces`, `mikrotik_ip_addresses`, `mikrotik_routes`,
  `mikrotik_arp`, `mikrotik_queues`, `mikrotik_ppp`, `mikrotik_users`, `mikrotik_hotspot`,
  `mikrotik_wireless`, `mikrotik_logs`; and the guarded raw `mikrotik_command` (the path for all writes).
- Claude Code plugin bundle (`plugin/`) with `mikrotik-admin` + `router-init` skills + `.mcp.json`.

> Read coverage for the requested object types (IP, routes, ARP, queues/tree, PPP, users & rights,
> hotspot users/profiles, WLAN interfaces & security) is in place via the curated tools above; **typed
> write tools** for these remain M3 (writes work today through `mikrotik_command`).

### M1 — Hardening & tests
- Unit tests against `tik4net.testing` `TikFakeConnection` (no live router): policy classification,
  resolver transport selection, result formatting.
- Integration smoke tests behind an opt-in flag (live router from env).
- Structured audit log (one record per call: timestamp, router, transport, command, write flag,
  outcome) with a configurable sink (stderr now; file/JSON later).
- CI (GitHub Actions): build + test + `dotnet pack`.

### M2 — Write workflow & guardrails
- **Dry-run / preview**: for writes, show the resolved command + a diff of intended change before
  applying (leveraging tik4net.entities `EntityDifference` / `SaveListDifferences`).
- **Approval gate**: optional "require confirmation" mode — a write returns a preview + token; a
  second `mikrotik_confirm` call applies it.
- **Path allowlist/denylist** and per-router rate limiting.
- **Backup-before-change** (`/system/backup` or export) + documented rollback.

### M3 — Domain coverage: **skills first, native tools only where they earn it**
Direction decided: rather than hand-code a typed C# tool per RouterOS object type, keep the server
thin and put domain expertise in **knowledge skills** that drive the guarded `mikrotik_command` and
link to the live docs at `manual.mikrotik.com`. An LLM already knows RouterOS syntax; it needs
judgment + current facts, which skills deliver more cheaply and maintainably than code.

- **Skills (done / ongoing):** `mikrotik-admin`, `router-init`, `mikrotik-firewall`, `mikrotik-ip`,
  `mikrotik-mangle-queue`, `mikrotik-home-wifi`, `mikrotik-vpn`, `mikrotik-hardening`,
  `mikrotik-monitoring`. Next candidates: CAPsMAN multi-AP, bridging/VLAN/switching, containers.
- **Native tools** stay limited to: the guarded raw command, discovery, inventory, and a few
  read conveniences (`RouterStateTools`). Add a *typed write* tool only when schema validation or
  safety on a specific high-risk write clearly justifies the maintenance cost.
- The prioritized, demand-grounded object-type list (and the `tik4net.entities`-exists-vs-gap audit)
  in [`native-entity-support.md`](native-entity-support.md) now serves mainly to guide **skill
  coverage** and to spot the rare object that warrants a native tool.

### M4 — Remote transport & deployment
- HTTP/SSE (streamable HTTP) transport for a centrally hosted, multi-user service.
- AuthN/AuthZ for the MCP endpoint; map MCP sessions to RBAC scopes.
- Container image + self-contained binaries; service/daemon mode; cross-platform config.

### M5 — Multi-tenant / RBAC
- Who may do what on which routers; per-agent scope; per-group policies.
- Integrate inventory/secrets with a real secret store (env → DPAPI/Key Vault/etc.).

### M6 — MCP-native features
- Router state as MCP **resources** (e.g. `mikrotik://core/ip/address`).
- **Prompt templates** for common workflows (audit firewall, set up a VLAN, diagnose an interface).
- Sampling where useful.

### M7 — Observability & docs
- Metrics + trace of AI actions (reuse tik4net RAW trace for protocol-level debugging).
- Admin-focused docs: quickstart, security recommendations, least-privilege RouterOS account recipe.

## Recommended additions (beyond the original sketch)

- **`mikrotik_command` raw trace toggle** — port the `includeRawTrace` option from the dev tool for
  protocol-level debugging from within an agent session.
- **Connectivity probe tool** — given a host/MAC, report which transports succeed (API/REST/WinBox/…),
  useful for onboarding a new router.
- **Safe Mode integration** — wrap risky write sessions in RouterOS Safe Mode so a lost connection
  auto-rolls-back (supported by tik4net CLI transports).
- **Config export/snapshot tool** — `/export` to a stored artifact for diffing and change review.
- **Redaction layer** — scrub secrets (passwords, pre-shared keys, private keys) from tool output
  before it reaches the model.
- **"Explain" mode** — for any write, return a human-readable description of the effect (pairs well
  with the approval gate).
- **MAC-layer recovery flow** — a guided tool combining MNDP discovery + MAC-Telnet/WinboxCliMac for
  routers with no IP (factory reset / bootstrap), a tik4net differentiator.

## Open items

- HTTP/SSE auth model (API keys vs OAuth) — decide before M4.
- Whether to depend on `tik4net.entities` now (richer semantic tools) or keep v1 on the low-level API.
- Plugin distribution channel: dotnet tool + `.mcpb` bundle + Claude Code plugin marketplace.
