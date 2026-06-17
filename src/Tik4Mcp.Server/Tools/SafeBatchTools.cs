using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Tik4Mcp.Server.Connections;
using Tik4Mcp.Server.Security;
using tik4net;

namespace Tik4Mcp.Server.Tools;

/// <summary>
/// One ordered, all-or-nothing batch of RouterOS commands run inside <b>Safe Mode</b> on a single
/// connection. Safe Mode is bound to the session: if the batch fails (or the connection drops)
/// before release, RouterOS rolls every change back. This is the only way tik4mcp can offer
/// auto-revert, because each <c>mikrotik_command</c> call opens its own short-lived connection and
/// cannot hold Safe Mode across calls.
/// </summary>
[McpServerToolType]
public sealed class SafeBatchTools
{
    private readonly ConnectionResolver _resolver;
    private readonly AccessPolicy _policy;
    private readonly ILogger<SafeBatchTools> _logger;

    public SafeBatchTools(ConnectionResolver resolver, AccessPolicy policy, ILogger<SafeBatchTools> logger)
    {
        _resolver = resolver;
        _policy = policy;
        _logger = logger;
    }

    [McpServerTool(Name = "mikrotik_safe_batch")]
    [Description(
        "Run an ordered list of RouterOS commands as ONE all-or-nothing transaction guarded by RouterOS Safe Mode. " +
        "Opens a single session, enters Safe Mode, runs every step, and releases (commits) only if all succeed; " +
        "if any step errors — or the connection drops mid-batch — RouterOS automatically ROLLS BACK every change. " +
        "Use this for multi-step, lockout-prone edits (firewall, addressing, routing) so a bad rule cannot strand you. " +
        "Set commit=false for a true dry-run: every step is applied and then rolled back, proving the commands are " +
        "accepted by the router without leaving any change behind. " +
        "Each step has 'command' (API path, e.g. /ip/firewall/filter/add) and optional 'parameters' (API words: " +
        "filters '?name=value', name-value '=name=value') — same form as mikrotik_command. " +
        "Requires a session transport (Api, ApiSsl, Telnet, MacTelnet, WinboxCli, WinboxCliMac, WinboxNative); REST cannot hold Safe Mode. " +
        "Writes are refused unless the router is writable. NOTE: Safe Mode reverts CONFIGURATION only — it does not undo " +
        "reboots, upgrades, resets, or file operations; keep those out of a batch. " +
        "Returns a JSON object with overall status, whether changes were committed, and each step's result.")]
    public string SafeBatch(
        [Description("Inventory router name to target. Omit when using ad-hoc host/username/password.")] string? router = null,
        [Description("Ordered steps to run in one Safe Mode transaction. Each: { command, parameters? }.")] SafeBatchStep[]? steps = null,
        [Description("If true (default) commit the batch on full success. If false, roll back after running all steps (dry-run).")] bool commit = true,
        [Description("Ad-hoc router host/IP (when 'router' is omitted).")] string? host = null,
        [Description("Ad-hoc username (when 'router' is omitted).")] string? username = null,
        [Description("Ad-hoc password (when 'router' is omitted).")] string? password = null,
        [Description("Transport override (case-insensitive): Api, ApiSsl, Telnet, MacTelnet, WinboxCli, WinboxCliMac, WinboxNative. REST is not supported (no Safe Mode).")] string? transport = null,
        [Description("TCP/UDP port override. 0 or omit = transport default.")] int port = 0,
        [Description("Router MAC 'AA:BB:CC:DD:EE:FF' for MacTelnet/WinboxCliMac. Omit to discover via MNDP.")] string? routerMac = null)
    {
        if (steps is null || steps.Length == 0)
            return "ERROR (argument): 'steps' must contain at least one command.";
        for (var i = 0; i < steps.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(steps[i]?.Command))
                return $"ERROR (argument): step {i} has an empty 'command'.";
        }

        ResolvedConnection? resolved = null;
        var held = false;
        try
        {
            resolved = _resolver.Open(
                router, host, username, password, transport, port > 0 ? port : null, routerMac);

            // Gate every step up front — refuse the whole batch if any write is blocked by policy.
            foreach (var step in steps)
                _policy.EnsureAllowed(step.Command, resolved.ReadOnly);

            if (!resolved.Connection.Supports(TikConnectionCapability.SafeMode))
                return $"ERROR (transport): transport '{resolved.Transport}' cannot hold Safe Mode. " +
                       "Use a session transport (Api, ApiSsl, Telnet, MacTelnet, WinboxCli, WinboxCliMac, WinboxNative) — not REST.";

            _logger.LogInformation("AUDIT safe_batch router={Router} transport={Transport} steps={Steps} commit={Commit}",
                resolved.RouterLabel, resolved.Transport, steps.Length, commit);

            resolved.Connection.SafeModeTake();
            held = true;

            var results = new List<Dictionary<string, object>>(steps.Length);
            var failed = false;
            var failedIndex = -1;

            for (var i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                var rows = new List<string> { step.Command };
                if (step.Parameters is { Length: > 0 })
                    rows.AddRange(step.Parameters);

                string result;
                var ok = true;
                try
                {
                    var sentences = resolved.Connection.CallCommandSync(rows).ToList();
                    result = TikResultFormatter.Format(sentences);
                    if (result.StartsWith("TRAP", StringComparison.Ordinal))
                        ok = false;
                }
                catch (TikCommandTrapException ex)
                {
                    result = $"TRAP: {ex.Message}";
                    ok = false;
                }
                catch (Exception ex)
                {
                    result = $"ERROR ({ex.GetType().Name}): {ex.Message}";
                    ok = false;
                }

                results.Add(new Dictionary<string, object>
                {
                    ["step"] = i,
                    ["command"] = step.Command,
                    ["ok"] = ok,
                    ["result"] = result,
                });

                if (!ok)
                {
                    failed = true;
                    failedIndex = i;
                    break;
                }
            }

            string status;
            bool committed;
            if (failed)
            {
                // Roll back in place where the transport allows; otherwise the finally/dispose drop reverts.
                committed = false;
                status = "rolled-back";
                TryRollback(resolved, ref held);
            }
            else if (commit)
            {
                resolved.Connection.SafeModeRelease();
                held = false;
                committed = true;
                status = "committed";
            }
            else
            {
                // Dry-run: everything applied cleanly, now discard it.
                TryRollback(resolved, ref held);
                committed = false;
                status = "dry-run-reverted";
            }

            return TikResultFormatter.ToJson(new Dictionary<string, object>
            {
                ["status"] = status,
                ["committed"] = committed,
                ["stepsRun"] = results.Count,
                ["stepsTotal"] = steps.Length,
                ["failedStep"] = failedIndex,
                ["steps"] = results,
            });
        }
        catch (AccessDeniedException ex)
        {
            return $"ERROR (policy): {ex.Message}";
        }
        catch (ArgumentException ex)
        {
            return $"ERROR (argument): {ex.Message}";
        }
        catch (NotSupportedException ex)
        {
            return $"ERROR (transport): {ex.Message}";
        }
        catch (TikConnectionLoginException ex)
        {
            return $"ERROR (auth): {ex.Message}";
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            return $"ERROR (network): {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"ERROR ({ex.GetType().Name}): {ex.Message}";
        }
        finally
        {
            // If we still hold Safe Mode here (unexpected/error path), do NOT release: disposing the
            // connection drops the session and RouterOS rolls the uncommitted changes back.
            resolved?.Dispose();
        }
    }

    /// <summary>
    /// Discards the in-flight Safe Mode changes immediately. Prefers in-place unroll; if the transport
    /// cannot unroll in place (native WinBox), leaves Safe Mode held so the connection drop reverts.
    /// </summary>
    private void TryRollback(ResolvedConnection resolved, ref bool held)
    {
        try
        {
            resolved.Connection.SafeModeUnroll();
            held = false;
        }
        catch (NotSupportedException)
        {
            // No in-place unroll on this transport — keep 'held' true so dispose drops the session and reverts.
            _logger.LogInformation("safe_batch rollback: in-place unroll unsupported on {Transport}; reverting via disconnect.",
                resolved.Transport);
        }
    }
}

/// <summary>One step of a <c>mikrotik_safe_batch</c>: a RouterOS command path plus its API words.</summary>
public sealed record SafeBatchStep
{
    [Description("RouterOS API command path, e.g. /ip/firewall/filter/add")]
    public string Command { get; init; } = "";

    [Description("Optional command words. Filters: '?name=value'. Name-value: '=name=value'.")]
    public string[]? Parameters { get; init; }
}
