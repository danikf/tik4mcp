using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Server;
using Tik4Mcp.Server.Connections;
using tik4net;

namespace Tik4Mcp.Server.Tools;

/// <summary>
/// Describes the connection transports tik4mcp can use: which FEATURES each supports (read live from
/// tik4net so they never drift), and what the router needs for each (IP or not, which RouterOS service
/// must be enabled, default port, certificate). Optionally annotates with the router's current
/// <c>/ip/service</c> state so the model can tell which transports will actually work.
/// </summary>
[McpServerToolType]
public sealed class ConnectionInfoTools
{
    private readonly ConnectionResolver _resolver;

    public ConnectionInfoTools(ConnectionResolver resolver)
    {
        _resolver = resolver;
    }

    private sealed record TransportSpec(
        TikConnectionType Type, bool RequiresIp, string RouterService,
        int DefaultPort, bool NeedsCertificate, string Notes);

    // Router-side requirements for each transport tik4mcp can open. The FEATURE set is read live from
    // tik4net (see ReadCapabilities); only these RouterOS facts are curated here.
    private static readonly TransportSpec[] Specs =
    {
        new(TikConnectionType.Api,          true,  "api",     8728, false, "Binary API. Enable IP > Services 'api'. The default transport."),
        new(TikConnectionType.ApiSsl,       true,  "api-ssl", 8729, true,  "TLS binary API. Enable 'api-ssl' AND assign a certificate to it first (see router-init)."),
        new(TikConnectionType.Rest,         true,  "www",     80,   false, "REST (RouterOS 7+), served by the 'www' service — enable it. Stateless: no Safe Mode."),
        new(TikConnectionType.RestSsl,      true,  "www-ssl", 443,  true,  "REST over TLS via 'www-ssl' — enable it and assign a certificate. Stateless: no Safe Mode."),
        new(TikConnectionType.Telnet,       true,  "telnet",  23,   false, "Plaintext CLI. Enable 'telnet' (insecure — avoid on production / WAN)."),
        new(TikConnectionType.WinboxCli,    true,  "winbox",  8291, false, "Encrypted WinBox channel, CLI mode. Enable 'winbox'."),
        new(TikConnectionType.WinboxNative, true,  "winbox",  8291, false, "Native WinBox M2 protocol. Enable 'winbox'."),
        new(TikConnectionType.MacTelnet,    false, "mac-server (MAC Telnet)", 0, false, "Layer-2 MAC-Telnet — NO IP NEEDED. Requires Tools > MAC Server enabled and the router on the same broadcast (L2) domain. Bootstrap/recovery; supply routerMac or discover via MNDP."),
        new(TikConnectionType.WinboxCliMac, false, "mac-server (MAC WinBox)", 0, false, "WinBox over the MAC layer — NO IP NEEDED. Requires Tools > MAC Server 'MAC WinBox' allowed. Bootstrap a router with no IP; supply routerMac or discover via MNDP."),
    };

    [McpServerTool(Name = "mikrotik_connection_info")]
    [Description(
        "Describe the transports tik4mcp can use to reach a router: each transport's supported FEATURES " +
        "(Crud, Listen, Streaming, SafeMode, RawCommand, …), whether it needs the router to have an IP, " +
        "which RouterOS service must be enabled (and its default port), and whether it needs a certificate. " +
        "Use this to pick a transport and to know what must be set up first: an IP transport (Api/ApiSsl/Rest/" +
        "RestSsl/Telnet/WinboxCli/WinboxNative) is unreachable until the router has an IP AND the service is " +
        "enabled and reachable (firewall/allowed-from); the MAC-layer transports (MacTelnet/WinboxCliMac) need " +
        "NO IP and are how you bootstrap or recover a router with no addressing. Note: only SafeMode-capable " +
        "transports can run mikrotik_safe_batch. Optionally pass 'router' or ad-hoc 'host' to also report which " +
        "IP services are CURRENTLY enabled on that router (/ip/service) so you can tell what will actually work. " +
        "Read-only.")]
    public string ConnectionInfo(
        [Description("Optional inventory router name — if given (and reachable), annotate with the router's current /ip/service state.")] string? router = null,
        [Description("Optional ad-hoc host/IP — same effect as 'router' for the live service check.")] string? host = null,
        [Description("Ad-hoc username for the optional live service check.")] string? username = null,
        [Description("Ad-hoc password for the optional live service check.")] string? password = null)
    {
        var transports = Specs.Select(spec => new Dictionary<string, object?>
        {
            ["transport"] = spec.Type.ToString(),
            ["features"] = ReadCapabilities(spec.Type),
            ["requiresIp"] = spec.RequiresIp,
            ["routerService"] = spec.RouterService,
            ["defaultPort"] = spec.DefaultPort == 0 ? null : spec.DefaultPort,
            ["needsCertificate"] = spec.NeedsCertificate,
            ["notes"] = spec.Notes,
        }).ToList();

        var result = new Dictionary<string, object?>
        {
            ["transports"] = transports,
            ["featureLegend"] = new Dictionary<string, string>
            {
                ["Crud"] = "create/read/update/delete of records (every transport)",
                ["Listen"] = "live change notifications (/path/listen)",
                ["Streaming"] = "streaming monitors (monitor-traffic, torch)",
                ["RawSentences"] = "raw !re/!done/!trap sentence access",
                ["Tagging"] = "tagged concurrent commands on one channel",
                ["SafeMode"] = "RouterOS Safe Mode bound to the connection (required by mikrotik_safe_batch)",
                ["RawCommand"] = "verbatim raw command pass-through",
            },
            ["note"] = "An IP transport needs the router to have an IP address AND the listed service enabled " +
                       "(IP > Services) and reachable from here. MAC-layer transports (MacTelnet, WinboxCliMac) " +
                       "need no IP — use them with a MAC from mikrotik_discover to bootstrap or recover a router.",
        };

        // Optional best-effort: the router's current IP-service state (which services are on/off + allowed-from).
        if (!string.IsNullOrWhiteSpace(router) || !string.IsNullOrWhiteSpace(host))
        {
            try
            {
                using var resolved = _resolver.Open(router, host, username, password);
                result["liveServices"] = resolved.Connection.CallCommandSync(new[] { "/ip/service/print" })
                    .OfType<ITikReSentence>()
                    .Select(re => new Dictionary<string, string>(re.Words))
                    .ToList();
            }
            catch (Exception ex)
            {
                result["liveServicesError"] = $"{ex.GetType().Name}: {ex.Message}";
            }
        }

        return TikResultFormatter.ToJson(result);
    }

    /// <summary>Reads a transport's feature set straight from tik4net (unopened connection), so it never drifts.</summary>
    private static object ReadCapabilities(TikConnectionType type)
    {
        try
        {
            using var conn = ConnectionFactory.CreateConnection(type);
            if (conn is ITikConnectionCapabilities caps)
                return Enum.GetValues(typeof(TikConnectionCapability))
                    .Cast<TikConnectionCapability>()
                    .Where(c => c != TikConnectionCapability.None && caps.Capabilities.HasFlag(c))
                    .Select(c => c.ToString())
                    .ToArray();
        }
        catch { /* fall through to unknown */ }
        return "unknown";
    }
}
