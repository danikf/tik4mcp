namespace Tik4Mcp.Server.Configuration;

/// <summary>
/// A single router in the inventory. The dictionary key in <see cref="Tik4McpOptions.Routers"/> is
/// the logical name; this type carries the connection details.
/// </summary>
public sealed class RouterProfile
{
    /// <summary>Host name or IP address. For MAC-only transports this may be left empty when <see cref="RouterMac"/> is set.</summary>
    public string Host { get; set; } = "";

    /// <summary>RouterOS user name. Prefer a least-privilege account dedicated to the AI agent.</summary>
    public string User { get; set; } = "";

    /// <summary>Password. Supply via environment variable / secret store, not a committed file.</summary>
    public string Password { get; set; } = "";

    /// <summary>
    /// Transport override (a <see cref="tik4net.TikConnectionType"/> name). When null the server's
    /// <see cref="Tik4McpOptions.DefaultTransport"/> is used.
    /// </summary>
    public string? Transport { get; set; }

    /// <summary>TCP/UDP port override. When null the transport default is used.</summary>
    public int? Port { get; set; }

    /// <summary>Router MAC ("AA:BB:CC:DD:EE:FF") for MAC-Telnet / WinboxCliMac. When null it is discovered via MNDP.</summary>
    public string? RouterMac { get; set; }

    /// <summary>Optional group/tag for organizing the inventory (e.g. "site-a", "lab"). Informational.</summary>
    public string? Group { get; set; }

    /// <summary>
    /// Per-router read-only flag. A router can only ever be writable when both this and the global
    /// <see cref="Tik4McpOptions.ReadOnly"/> allow it. Default true (read-only).
    /// </summary>
    public bool ReadOnly { get; set; } = true;

    /// <summary>Free-text note surfaced to the AI when listing the inventory (e.g. role, location).</summary>
    public string? Notes { get; set; }
}
