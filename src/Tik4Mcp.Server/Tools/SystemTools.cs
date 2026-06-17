using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Server;
using Tik4Mcp.Server.Connections;
using tik4net;

namespace Tik4Mcp.Server.Tools;

/// <summary>
/// Curated, read-only "semantic" tools for the most common admin lookups. They wrap fixed RouterOS
/// print paths so the AI does not have to know the API syntax for routine questions. For anything
/// outside this set, fall back to <c>mikrotik_command</c>.
/// </summary>
[McpServerToolType]
public sealed class SystemTools
{
    private readonly ConnectionResolver _resolver;

    public SystemTools(ConnectionResolver resolver)
    {
        _resolver = resolver;
    }

    [McpServerTool(Name = "mikrotik_system_overview")]
    [Description("Health snapshot of a router: identity, RouterOS version/resource usage (CPU, memory, uptime) and routerboard model. Read-only.")]
    public string SystemOverview(
        [Description("Inventory router name. Omit when using ad-hoc host/username/password.")] string? router = null,
        [Description("Ad-hoc host/IP.")] string? host = null,
        [Description("Ad-hoc username.")] string? username = null,
        [Description("Ad-hoc password.")] string? password = null)
    {
        return Run(router, host, username, password, conn =>
        {
            var identity = ReadRecords(conn, "/system/identity/print").FirstOrDefault();
            var resource = ReadRecords(conn, "/system/resource/print").FirstOrDefault();
            var routerboard = ReadRecords(conn, "/system/routerboard/print").FirstOrDefault();
            return TikResultFormatter.ToJson(new { identity, resource, routerboard });
        });
    }

    [McpServerTool(Name = "mikrotik_interfaces")]
    [Description("List the router's interfaces with type, running/disabled state and traffic counters. Read-only.")]
    public string Interfaces(
        [Description("Inventory router name. Omit when using ad-hoc host/username/password.")] string? router = null,
        [Description("Ad-hoc host/IP.")] string? host = null,
        [Description("Ad-hoc username.")] string? username = null,
        [Description("Ad-hoc password.")] string? password = null)
        => Run(router, host, username, password,
            conn => TikResultFormatter.ToJson(ReadRecords(conn, "/interface/print")));

    [McpServerTool(Name = "mikrotik_ip_addresses")]
    [Description("List the IPv4 addresses configured on the router and their interfaces. Read-only.")]
    public string IpAddresses(
        [Description("Inventory router name. Omit when using ad-hoc host/username/password.")] string? router = null,
        [Description("Ad-hoc host/IP.")] string? host = null,
        [Description("Ad-hoc username.")] string? username = null,
        [Description("Ad-hoc password.")] string? password = null)
        => Run(router, host, username, password,
            conn => TikResultFormatter.ToJson(ReadRecords(conn, "/ip/address/print")));

    [McpServerTool(Name = "mikrotik_logs")]
    [Description("Return the most recent router log entries (time, topics, message). Read-only.")]
    public string Logs(
        [Description("Inventory router name. Omit when using ad-hoc host/username/password.")] string? router = null,
        [Description("Maximum entries to return (most recent first). Default 50.")] int limit = 50,
        [Description("Ad-hoc host/IP.")] string? host = null,
        [Description("Ad-hoc username.")] string? username = null,
        [Description("Ad-hoc password.")] string? password = null)
        => Run(router, host, username, password, conn =>
        {
            var records = ReadRecords(conn, "/log/print");
            if (limit > 0 && records.Count > limit)
                records = records.Skip(records.Count - limit).ToList();
            return TikResultFormatter.ToJson(records);
        });

    // ── helpers ──────────────────────────────────────────────────────────────

    private string Run(string? router, string? host, string? username, string? password,
        Func<ITikConnection, string> body)
    {
        try
        {
            using var resolved = _resolver.Open(router, host, username, password);
            return body(resolved.Connection);
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
        catch (Exception ex)
        {
            return $"ERROR ({ex.GetType().Name}): {ex.Message}";
        }
    }

    private static List<Dictionary<string, string>> ReadRecords(ITikConnection conn, string path)
        => conn.CallCommandSync(new[] { path })
            .OfType<ITikReSentence>()
            .Select(re => new Dictionary<string, string>(re.Words))
            .ToList();
}
