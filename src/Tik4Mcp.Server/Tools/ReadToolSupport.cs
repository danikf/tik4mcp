using System;
using System.Collections.Generic;
using System.Linq;
using Tik4Mcp.Server.Connections;
using tik4net;

namespace Tik4Mcp.Server.Tools;

/// <summary>Shared plumbing for the curated read-only tools: open, run a print, format, catch.</summary>
internal static class ReadToolSupport
{
    /// <summary>Opens the target, runs a single print <paramref name="path"/>, returns records as JSON.</summary>
    public static string ReadPath(
        ConnectionResolver resolver,
        string? router, string? host, string? username, string? password,
        string path)
    {
        try
        {
            using var resolved = resolver.Open(router, host, username, password);
            var records = resolved.Connection.CallCommandSync(new[] { path })
                .OfType<ITikReSentence>()
                .Select(re => new Dictionary<string, string>(re.Words))
                .ToList();
            return TikResultFormatter.ToJson(records);
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

    /// <summary>
    /// Maps a caller-supplied section name (case-insensitive) to a print path from
    /// <paramref name="sections"/>, or returns an <c>ERROR (argument)</c> string in <paramref name="error"/>.
    /// </summary>
    public static bool TryResolveSection(
        IReadOnlyDictionary<string, string> sections, string? section, out string path, out string error)
    {
        path = "";
        error = "";
        var key = (section ?? "").Trim().ToLowerInvariant();
        if (sections.TryGetValue(key, out var p))
        {
            path = p;
            return true;
        }
        error = $"ERROR (argument): unknown section '{section}'. Valid: {string.Join(", ", sections.Keys)}.";
        return false;
    }
}
