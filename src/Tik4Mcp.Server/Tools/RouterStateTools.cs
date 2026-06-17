using System.Collections.Generic;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Tik4Mcp.Server.Connections;

namespace Tik4Mcp.Server.Tools;

/// <summary>
/// Curated read-only tools for the most-requested RouterOS object types (see
/// docs/native-entity-support.md): routing, ARP, queues, PPP, users/rights, hotspot, and wireless.
/// Closely related sub-objects share one tool via a section/kind parameter to keep the surface tight.
/// All are read-only; create/modify/delete go through the guarded <c>mikrotik_command</c> tool.
/// </summary>
[McpServerToolType]
public sealed class RouterStateTools
{
    private readonly ConnectionResolver _resolver;

    public RouterStateTools(ConnectionResolver resolver)
    {
        _resolver = resolver;
    }

    // ── Routing & ARP ─────────────────────────────────────────────────────────

    [McpServerTool(Name = "mikrotik_routes")]
    [Description("List IPv4 routes (/ip/route): dst-address, gateway, distance, and active/dynamic/static flags. Read-only; change routes via mikrotik_command (/ip/route/add|set|remove).")]
    public string Routes(
        [Description("Inventory router name. Omit for ad-hoc host/username/password.")] string? router = null,
        [Description("Ad-hoc host/IP.")] string? host = null,
        [Description("Ad-hoc username.")] string? username = null,
        [Description("Ad-hoc password.")] string? password = null)
        => ReadToolSupport.ReadPath(_resolver, router, host, username, password, "/ip/route/print");

    [McpServerTool(Name = "mikrotik_arp")]
    [Description("List the IPv4 ARP table (/ip/arp): address, mac-address, interface, and dynamic/complete flags. Read-only.")]
    public string Arp(
        [Description("Inventory router name. Omit for ad-hoc host/username/password.")] string? router = null,
        [Description("Ad-hoc host/IP.")] string? host = null,
        [Description("Ad-hoc username.")] string? username = null,
        [Description("Ad-hoc password.")] string? password = null)
        => ReadToolSupport.ReadPath(_resolver, router, host, username, password, "/ip/arp/print");

    // ── Queues / QoS ───────────────────────────────────────────────────────────

    private static readonly Dictionary<string, string> QueueKinds = new()
    {
        ["simple"] = "/queue/simple/print",
        ["tree"] = "/queue/tree/print",
        ["type"] = "/queue/type/print",
    };

    [McpServerTool(Name = "mikrotik_queues")]
    [Description("List traffic queues. kind=simple (/queue/simple, per-target bandwidth limits), tree (/queue/tree, hierarchical QoS) or type (/queue/type). Read-only; modify via mikrotik_command.")]
    public string Queues(
        [Description("Which queue table: 'simple' (default), 'tree', or 'type'.")] string kind = "simple",
        [Description("Inventory router name. Omit for ad-hoc host/username/password.")] string? router = null,
        [Description("Ad-hoc host/IP.")] string? host = null,
        [Description("Ad-hoc username.")] string? username = null,
        [Description("Ad-hoc password.")] string? password = null)
    {
        if (!ReadToolSupport.TryResolveSection(QueueKinds, kind, out var path, out var error))
            return error;
        return ReadToolSupport.ReadPath(_resolver, router, host, username, password, path);
    }

    // ── PPP (PPPoE / VPN users) ─────────────────────────────────────────────────

    private static readonly Dictionary<string, string> PppSections = new()
    {
        ["secret"] = "/ppp/secret/print",   // user accounts
        ["secrets"] = "/ppp/secret/print",
        ["profile"] = "/ppp/profile/print", // defaults referenced by secrets
        ["profiles"] = "/ppp/profile/print",
        ["active"] = "/ppp/active/print",   // currently connected sessions
    };

    [McpServerTool(Name = "mikrotik_ppp")]
    [Description("List PPP objects for PPPoE/L2TP/VPN. section=secret (user accounts, /ppp/secret), profile (defaults, /ppp/profile) or active (live sessions, /ppp/active). Read-only; create users via mikrotik_command (/ppp/secret/add).")]
    public string Ppp(
        [Description("Which PPP table: 'secret' (default), 'profile', or 'active'.")] string section = "secret",
        [Description("Inventory router name. Omit for ad-hoc host/username/password.")] string? router = null,
        [Description("Ad-hoc host/IP.")] string? host = null,
        [Description("Ad-hoc username.")] string? username = null,
        [Description("Ad-hoc password.")] string? password = null)
    {
        if (!ReadToolSupport.TryResolveSection(PppSections, section, out var path, out var error))
            return error;
        return ReadToolSupport.ReadPath(_resolver, router, host, username, password, path);
    }

    // ── Users & rights ──────────────────────────────────────────────────────────

    private static readonly Dictionary<string, string> UserSections = new()
    {
        ["users"] = "/user/print",
        ["user"] = "/user/print",
        ["groups"] = "/user/group/print",   // rights / permission sets
        ["group"] = "/user/group/print",
        ["rights"] = "/user/group/print",
    };

    [McpServerTool(Name = "mikrotik_users")]
    [Description("List router login accounts and their rights. section=users (/user: accounts, groups, disabled state) or groups (/user/group: permission policies). Read-only; manage via mikrotik_command (/user/add|set|disable, /user/group/add|set).")]
    public string Users(
        [Description("Which table: 'users' (default) or 'groups' (permission policies / rights).")] string section = "users",
        [Description("Inventory router name. Omit for ad-hoc host/username/password.")] string? router = null,
        [Description("Ad-hoc host/IP.")] string? host = null,
        [Description("Ad-hoc username.")] string? username = null,
        [Description("Ad-hoc password.")] string? password = null)
    {
        if (!ReadToolSupport.TryResolveSection(UserSections, section, out var path, out var error))
            return error;
        return ReadToolSupport.ReadPath(_resolver, router, host, username, password, path);
    }

    // ── Hotspot ───────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, string> HotspotSections = new()
    {
        ["users"] = "/ip/hotspot/user/print",
        ["user"] = "/ip/hotspot/user/print",
        ["profiles"] = "/ip/hotspot/user/profile/print",
        ["profile"] = "/ip/hotspot/user/profile/print",
        ["active"] = "/ip/hotspot/active/print",
        ["bindings"] = "/ip/hotspot/ip-binding/print",
        ["ip-binding"] = "/ip/hotspot/ip-binding/print",
    };

    [McpServerTool(Name = "mikrotik_hotspot")]
    [Description("List hotspot objects. section=users (/ip/hotspot/user), profiles (/ip/hotspot/user/profile — rate-limits/shared-users), active (live logins) or bindings (/ip/hotspot/ip-binding). Read-only; manage via mikrotik_command.")]
    public string Hotspot(
        [Description("Which table: 'users' (default), 'profiles', 'active', or 'bindings'.")] string section = "users",
        [Description("Inventory router name. Omit for ad-hoc host/username/password.")] string? router = null,
        [Description("Ad-hoc host/IP.")] string? host = null,
        [Description("Ad-hoc username.")] string? username = null,
        [Description("Ad-hoc password.")] string? password = null)
    {
        if (!ReadToolSupport.TryResolveSection(HotspotSections, section, out var path, out var error))
            return error;
        return ReadToolSupport.ReadPath(_resolver, router, host, username, password, path);
    }

    // ── Wireless (WLAN) ──────────────────────────────────────────────────────────

    private static readonly Dictionary<string, string> WirelessSections = new()
    {
        ["interfaces"] = "/interface/wireless/print",
        ["interface"] = "/interface/wireless/print",
        ["security"] = "/interface/wireless/security-profiles/print",   // the "secrets" (WPA keys)
        ["security-profiles"] = "/interface/wireless/security-profiles/print",
        ["secrets"] = "/interface/wireless/security-profiles/print",
        ["registration"] = "/interface/wireless/registration-table/print",
        ["clients"] = "/interface/wireless/registration-table/print",
    };

    [McpServerTool(Name = "mikrotik_wireless")]
    [Description("List legacy WLAN objects. section=interfaces (/interface/wireless: SSID/band/channel), security (/interface/wireless/security-profiles: WPA modes and pre-shared keys — the wireless 'secrets') or registration (connected clients). Read-only; modify via mikrotik_command. Note: RouterOS 7.13+ 'wifiwave2' lives under /interface/wifi — query it with mikrotik_command.")]
    public string Wireless(
        [Description("Which table: 'interfaces' (default), 'security' (security-profiles / keys), or 'registration' (clients).")] string section = "interfaces",
        [Description("Inventory router name. Omit for ad-hoc host/username/password.")] string? router = null,
        [Description("Ad-hoc host/IP.")] string? host = null,
        [Description("Ad-hoc username.")] string? username = null,
        [Description("Ad-hoc password.")] string? password = null)
    {
        if (!ReadToolSupport.TryResolveSection(WirelessSections, section, out var path, out var error))
            return error;
        return ReadToolSupport.ReadPath(_resolver, router, host, username, password, path);
    }
}
