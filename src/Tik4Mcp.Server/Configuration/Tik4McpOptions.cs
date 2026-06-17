using System.Collections.Generic;

namespace Tik4Mcp.Server.Configuration;

/// <summary>
/// Root configuration for the tik4mcp server. Bound from the "Tik4Mcp" configuration section
/// (appsettings.json) and overridable via <c>TIK4MCP_</c>-prefixed environment variables, e.g.
/// <c>TIK4MCP_Routers__core__Password</c>. Credentials should live in environment variables / a
/// secret store, never in a committed appsettings.json.
/// </summary>
public sealed class Tik4McpOptions
{
    public const string SectionName = "Tik4Mcp";

    /// <summary>
    /// Global read-only switch. When true (default) the server refuses every write command
    /// (add/set/remove/move/…) regardless of router. A router may relax this per-profile, but only
    /// when this global flag is also false. Flip to false to allow writes.
    /// </summary>
    public bool ReadOnly { get; set; } = true;

    /// <summary>Connect timeout applied to every connection, in seconds.</summary>
    public int ConnectTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Default transport used when a router profile (or an ad-hoc call) does not specify one.
    /// One of the <see cref="tik4net.TikConnectionType"/> names, e.g. "Api", "ApiSsl", "Rest".
    /// </summary>
    public string DefaultTransport { get; set; } = "Api";

    /// <summary>
    /// When true, tools accept ad-hoc host/user/password parameters for routers that are not in the
    /// inventory. When false, only named inventory routers may be reached. Default true.
    /// </summary>
    public bool AllowAdhoc { get; set; } = true;

    /// <summary>
    /// Named router inventory, keyed by a short logical name the AI refers to (e.g. "core", "edge-1").
    /// </summary>
    public Dictionary<string, RouterProfile> Routers { get; set; }
        = new(System.StringComparer.OrdinalIgnoreCase);
}
