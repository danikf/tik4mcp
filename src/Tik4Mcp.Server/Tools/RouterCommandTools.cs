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
/// The raw, guarded command surface: run any RouterOS API command over any transport against an
/// inventory router or an ad-hoc target. Write commands are blocked unless the router is writable.
/// </summary>
[McpServerToolType]
public sealed class RouterCommandTools
{
    private readonly ConnectionResolver _resolver;
    private readonly AccessPolicy _policy;
    private readonly ILogger<RouterCommandTools> _logger;

    public RouterCommandTools(ConnectionResolver resolver, AccessPolicy policy, ILogger<RouterCommandTools> logger)
    {
        _resolver = resolver;
        _policy = policy;
        _logger = logger;
    }

    [McpServerTool(Name = "mikrotik_command")]
    [Description(
        "Run a RouterOS API command against a MikroTik router and return its result as JSON. " +
        "Target the router by inventory name ('router') OR ad-hoc ('host'/'username'/'password'). " +
        "Command paths use MikroTik API form, e.g. '/system/resource/print', '/ip/address/print', '/ip/address/add'. " +
        "Parameters use API word form: filters start with '?' (e.g. '?disabled=yes'); name-value words start with '=' " +
        "(e.g. '=address=192.168.1.1/24'). Write commands (add/set/remove/move/…) are refused when the router is read-only. " +
        "Returns a JSON array of records, 'OK (no data returned)', or an ERROR/TRAP message.")]
    public string Command(
        [Description("Inventory router name to target. Omit when using ad-hoc host/username/password.")] string? router = null,
        [Description("API command path, e.g. /ip/address/print or /ip/address/add")] string command = "",
        [Description("Optional command words. Filters: '?name=value'. Name-value: '=name=value'.")] string[]? parameters = null,
        [Description("Ad-hoc router host/IP (when 'router' is omitted).")] string? host = null,
        [Description("Ad-hoc username (when 'router' is omitted).")] string? username = null,
        [Description("Ad-hoc password (when 'router' is omitted).")] string? password = null,
        [Description("Transport override (case-insensitive): Api, ApiSsl, Rest, RestSsl, Telnet, MacTelnet, WinboxCli, WinboxCliMac, WinboxNative. Omit to use the router/default transport.")] string? transport = null,
        [Description("TCP/UDP port override. 0 or omit = transport default.")] int port = 0,
        [Description("Router MAC 'AA:BB:CC:DD:EE:FF' for MacTelnet/WinboxCliMac. Omit to discover via MNDP.")] string? routerMac = null)
    {
        if (string.IsNullOrWhiteSpace(command))
            return "ERROR (argument): 'command' is required.";

        try
        {
            using var resolved = _resolver.Open(
                router, host, username, password, transport, port > 0 ? port : null, routerMac);

            _policy.EnsureAllowed(command, resolved.ReadOnly);

            var commandRows = new List<string> { command };
            if (parameters is { Length: > 0 })
                commandRows.AddRange(parameters);

            var isWrite = _policy.IsWriteCommand(command);
            _logger.LogInformation("AUDIT router={Router} transport={Transport} write={Write} command={Command}",
                resolved.RouterLabel, resolved.Transport, isWrite, command);

            var sentences = resolved.Connection.CallCommandSync(commandRows).ToList();
            return TikResultFormatter.Format(sentences);
        }
        catch (AccessDeniedException ex)
        {
            return $"ERROR (policy): {ex.Message}";
        }
        catch (ArgumentException ex)
        {
            return $"ERROR (argument): {ex.Message}";
        }
        catch (TikConnectionLoginException ex)
        {
            return $"ERROR (auth): {ex.Message}";
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            return $"ERROR (network): {ex.Message}";
        }
        catch (TikCommandTrapException ex)
        {
            return $"ERROR (trap): {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"ERROR ({ex.GetType().Name}): {ex.Message}";
        }
    }
}
