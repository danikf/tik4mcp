using System;
using Microsoft.Extensions.Options;
using Tik4Mcp.Server.Configuration;
using tik4net;

namespace Tik4Mcp.Server.Connections;

/// <summary>
/// Turns a router reference — either a named inventory entry or ad-hoc connection parameters — into
/// an open <see cref="ITikConnection"/>, and reports the effective read-only state for that router.
/// All transport selection lives here.
/// </summary>
public sealed class ConnectionResolver
{
    private readonly Tik4McpOptions _options;

    public ConnectionResolver(IOptions<Tik4McpOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Resolves and opens a connection. Provide <paramref name="router"/> to use a named inventory
    /// entry, or the ad-hoc <paramref name="host"/>/<paramref name="user"/>/<paramref name="password"/>
    /// for a one-off target. Any non-null override (transport/port/routerMac) wins over the profile.
    /// The caller owns the returned connection and must dispose it.
    /// </summary>
    public ResolvedConnection Open(
        string? router = null,
        string? host = null,
        string? user = null,
        string? password = null,
        string? transport = null,
        int? port = null,
        string? routerMac = null)
    {
        RouterProfile profile;
        string routerLabel;

        if (!string.IsNullOrWhiteSpace(router))
        {
            if (!_options.Routers.TryGetValue(router, out var found))
                throw new ArgumentException(
                    $"Unknown router '{router}'. Known routers: {string.Join(", ", _options.Routers.Keys)}");
            profile = found;
            routerLabel = router;
        }
        else
        {
            if (!_options.AllowAdhoc)
                throw new ArgumentException(
                    "Ad-hoc connections are disabled (Tik4Mcp:AllowAdhoc=false). Use a named router.");
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Provide either 'router' (an inventory name) or 'host' for an ad-hoc connection.");

            profile = new RouterProfile
            {
                Host = host!,
                User = user ?? "",
                Password = password ?? "",
                ReadOnly = true, // ad-hoc targets are always read-only unless globally writable
            };
            routerLabel = host!;
        }

        var transportName = transport ?? profile.Transport ?? _options.DefaultTransport;
        if (!Enum.TryParse<TikConnectionType>(transportName, ignoreCase: true, out var transportType))
            throw new ArgumentException($"Unknown transport '{transportName}'.");

        var effectivePort = port ?? profile.Port;
        var effectiveMac = routerMac ?? profile.RouterMac;

        var setup = new TikConnectionSetup(profile.Host, profile.User, profile.Password)
        {
            ConnectTimeout = TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds),
        };
        if (effectivePort.HasValue)
            setup.Port = effectivePort.Value;

        var connection = OpenForTransport(setup, transportType, effectiveMac);

        // A router is writable only when BOTH the global switch and the profile allow it.
        var effectiveReadOnly = _options.ReadOnly || profile.ReadOnly;

        return new ResolvedConnection(connection, routerLabel, transportType, effectiveReadOnly);
    }

    private static ITikConnection OpenForTransport(
        TikConnectionSetup setup, TikConnectionType transportType, string? routerMac)
    {
        return transportType switch
        {
            TikConnectionType.Api => setup.CreateApiConnection(),
            TikConnectionType.ApiSsl => setup.CreateApiSslConnection(),
            TikConnectionType.Rest => setup.CreateRestConnection(),
            TikConnectionType.RestSsl => setup.CreateRestSslConnection(),
            TikConnectionType.Telnet => setup.CreateTelnetConnection(),
            TikConnectionType.MacTelnet => setup.CreateMacTelnetConnection(routerMac),
            TikConnectionType.WinboxCli => setup.CreateWinboxCliConnection(),
            TikConnectionType.WinboxCliMac => setup.CreateWinboxCliMacConnection(routerMac),
            TikConnectionType.WinboxNative => setup.CreateWinboxNativeConnection(),
            _ => throw new ArgumentException(
                $"Transport '{transportType}' is not supported by tik4mcp (SSH needs the tik4net.ssh package)."),
        };
    }
}

/// <summary>An open connection plus the metadata tools need to enforce policy and report results.</summary>
public sealed class ResolvedConnection : IDisposable
{
    public ResolvedConnection(ITikConnection connection, string routerLabel, TikConnectionType transport, bool readOnly)
    {
        Connection = connection;
        RouterLabel = routerLabel;
        Transport = transport;
        ReadOnly = readOnly;
    }

    public ITikConnection Connection { get; }
    public string RouterLabel { get; }
    public TikConnectionType Transport { get; }
    public bool ReadOnly { get; }

    public void Dispose() => Connection.Dispose();
}
