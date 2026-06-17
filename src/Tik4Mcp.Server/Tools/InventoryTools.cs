using System.ComponentModel;
using System.Linq;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Tik4Mcp.Server.Configuration;

namespace Tik4Mcp.Server.Tools;

/// <summary>Lets the AI discover which named routers it may target — without ever exposing credentials.</summary>
[McpServerToolType]
public sealed class InventoryTools
{
    private readonly Tik4McpOptions _options;

    public InventoryTools(IOptions<Tik4McpOptions> options)
    {
        _options = options.Value;
    }

    [McpServerTool(Name = "mikrotik_list_routers")]
    [Description(
        "List the configured MikroTik routers this server can reach (name, host, transport, group, notes, " +
        "and whether the router is writable). Passwords are never returned. Use a returned name as the " +
        "'router' argument of other tools.")]
    public string ListRouters()
    {
        var routers = _options.Routers.Select(kvp => new
        {
            name = kvp.Key,
            host = kvp.Value.Host,
            transport = kvp.Value.Transport ?? _options.DefaultTransport,
            group = kvp.Value.Group,
            readOnly = _options.ReadOnly || kvp.Value.ReadOnly,
            notes = kvp.Value.Notes,
        }).ToList();

        return TikResultFormatter.ToJson(new
        {
            globalReadOnly = _options.ReadOnly,
            allowAdhoc = _options.AllowAdhoc,
            defaultTransport = _options.DefaultTransport,
            routers,
        });
    }
}
