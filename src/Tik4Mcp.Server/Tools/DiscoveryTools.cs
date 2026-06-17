using System;
using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Server;
using tik4net.Mndp;

namespace Tik4Mcp.Server.Tools;

/// <summary>Layer-2 discovery: find MikroTik routers on the local network via MNDP, no IP required.</summary>
[McpServerToolType]
public sealed class DiscoveryTools
{
    [McpServerTool(Name = "mikrotik_discover")]
    [Description(
        "Discover MikroTik routers on the local network segment via MNDP (MikroTik Neighbor Discovery " +
        "Protocol, UDP broadcast). Returns identity, version, board, MAC, IPv4/IPv6 and interface for each " +
        "neighbour found. Works without an IP route — the MAC can be fed to MacTelnet/WinboxCliMac. " +
        "Discovery listens for the given number of seconds (default 5).")]
    public string Discover(
        [Description("How long to listen for MNDP replies, in seconds (1-60). Default 5.")] int timeoutSeconds = 5,
        [Description("Stop as soon as the first router answers. Default false.")] bool stopOnFirst = false)
    {
        timeoutSeconds = Math.Clamp(timeoutSeconds, 1, 60);

        var neighbours = MndpHelper
            .Discover(TimeSpan.FromSeconds(timeoutSeconds), System.Text.Encoding.GetEncoding("iso-8859-1"), stopOnFirst)
            .Select(d => new
            {
                identity = d.Identity,
                version = d.Version,
                platform = d.Platform,
                board = d.BoardName,
                mac = d.Mac,
                ipv4 = d.IPv4?.ToString(),
                ipv6 = d.IPv6,
                interfaceName = d.InterfaceName,
                uptime = d.Uptime.ToString(),
                softwareId = d.SoftwareId,
            })
            .ToList();

        return neighbours.Count == 0
            ? "No MikroTik routers answered MNDP within the timeout."
            : TikResultFormatter.ToJson(neighbours);
    }
}
