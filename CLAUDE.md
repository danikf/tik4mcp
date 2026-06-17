# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

tik4mcp is an **MCP (Model Context Protocol) server** that lets AI assistants administer MikroTik
RouterOS routers. It is built on the [tik4net](https://github.com/danikf/tik4net) library (consumed as
the NuGet package `tik4net` **4.0.0-alpha**, pinned via `$(Tik4NetVersion)` in
[Directory.Build.props](Directory.Build.props)) and inherits every tik4net transport.

This is the **admin-grade** counterpart to the small `Tools/tik4net.mcp` debug tool that lives in the
tik4net repo: it adds a router inventory, read-only-by-default guardrails, an audit log, MNDP
discovery, and curated semantic tools.

## Build / run / test

```bash
dotnet build tik4mcp.slnx                      # build (note: .slnx, the modern XML solution format)
dotnet run --project src/Tik4Mcp.Server        # run as a stdio MCP server
dotnet pack src/Tik4Mcp.Server                 # pack the `tik4mcp` dotnet tool
```

There is no test project yet (planned, see `docs/development-plan.md` M1). When added, tests should use
`tik4net.testing`'s `TikFakeConnection` so they run without a live router.

Smoke-test the server without an MCP client by piping a JSON-RPC handshake to stdin (initialize →
notifications/initialized → tools/list). Note: on Windows/git-bash, capturing the server's stdout
through a pipe is unreliable; confirm behavior from the stderr handler logs instead.

## Architecture

Single project: `src/Tik4Mcp.Server` (`net8.0`, assembly/tool name `tik4mcp`). Request flow:

```
MCP client ──stdio──> Program.cs (host + config + DI)
                        ├─ Configuration/  Tik4McpOptions + RouterProfile  (inventory model)
                        ├─ Connections/    ConnectionResolver              (opens tik4net connections)
                        ├─ Security/        AccessPolicy                    (read/write guardrail)
                        └─ Tools/           [McpServerToolType] classes     (the AI-facing tools)
```

Two chokepoints everything funnels through — preserve this when adding features:

- **`ConnectionResolver.Open(...)`** is the *only* place that opens a connection. It resolves a target
  (named inventory router **or** ad-hoc host/user/password), picks the transport, and returns a
  `ResolvedConnection` carrying the open `ITikConnection`, the router label, and the **effective
  read-only** state. Per-transport `Create*Connection` mapping lives here. SSH is intentionally not
  wired (it needs the separate `tik4net.ssh` package).
- **`AccessPolicy`** classifies a command as read vs write (by the trailing API path segment) and
  enforces read-only. A router is writable only when **both** the global `Tik4Mcp:ReadOnly` and that
  router's `RouterProfile.ReadOnly` are `false`. Any new write path must call `EnsureAllowed`.

### Configuration & secrets

Bound from `appsettings.json` section `Tik4Mcp` (`Tik4McpOptions.SectionName`), overlaid by
`TIK4MCP_`-prefixed environment variables (`builder.Configuration.AddEnvironmentVariables("TIK4MCP_")`).
The `Routers` inventory is a **dictionary keyed by logical name**, so a secret overrides cleanly as
`TIK4MCP_Routers__<name>__Password`. **Never commit real credentials to appsettings.json.**

### Adding a tool

1. Add a method to an existing `[McpServerToolType]` class in `Tools/`, or create a new such class.
2. Annotate with `[McpServerTool(Name = "mikrotik_...")]` and a rich `[Description]` (the model relies
   on it). Tool names are `snake_case`, prefixed `mikrotik_`.
3. Register new tool classes in `Program.cs` via `.WithTools<T>()`. Tool classes get their
   dependencies (`ConnectionResolver`, `AccessPolicy`, `ILogger<T>`) by constructor injection.
4. Open connections only through `ConnectionResolver`; gate writes through `AccessPolicy`; format
   results with `TikResultFormatter`. Tools return a JSON string (or an `ERROR (...)`/`TRAP ...`
   string) — they catch their own exceptions rather than throwing across the MCP boundary.

## tik4net coupling

The tik4net source is checked out locally at `../../tik4net/tik4net` (the maintainer owns it). v1
consumes the published `4.0.0-alpha` NuGet, not a project/submodule reference — so a needed tik4net
fix means publishing a new alpha and bumping `$(Tik4NetVersion)`. Key tik4net entry points used here:
`TikConnectionSetup` (+ `Create*Connection` factories), `ITikConnection.CallCommandSync`, the
`ITikReSentence`/`ITikTrapSentence` result sentences, and `tik4net.Mndp.MndpHelper` for discovery.

## Distribution

Shipped both as a dotnet tool (`tik4mcp`) and as a **Claude Code plugin** under `plugin/`
(`.claude-plugin/plugin.json`, `.mcp.json`, and skills under `plugin/skills/`: `mikrotik-admin`,
`router-init`, `mikrotik-firewall`, `mikrotik-ip`, `mikrotik-mangle-queue`). Keep the plugin's tool
descriptions and the skills' guidance in sync with the actual tools in `src/`.

**Design direction — skills over native tools.** The strategic bet is a *thin server*
(one guarded `mikrotik_command` + safety/inventory/discovery + a few read conveniences) plus *rich
skills* that carry RouterOS best-practices and link to the live docs at `manual.mikrotik.com` (the
old `help.mikrotik.com`/`wiki.mikrotik.com` are frozen). An LLM already knows RouterOS API syntax;
what it needs is domain judgment and current facts — cheaper and more maintainable to put in skills
than to hand-code a typed C# tool per object type. Add native tools only where schema/safety on a
specific high-risk write clearly justifies it. When adding/maintaining skills, verify doc links
resolve on `manual.mikrotik.com` (its URL scheme is `/docs/<section>/<page>`).

See `docs/development-plan.md` for the milestone roadmap and locked v1 decisions, and
`docs/native-entity-support.md` for the prioritized, demand-grounded list of RouterOS object types to
support natively (with the tik4net.entities-exists-vs-gap audit) — this drives M3 and phase-1.

Note: the `router-init` flow needs the MAC-layer ad-hoc path in `ConnectionResolver` (host falls back
to `routerMac` when only a MAC is supplied) and requires the server to run with writes enabled
(`TIK4MCP_ReadOnly=false`).
