using System;
using System.Linq;

namespace Tik4Mcp.Server.Security;

/// <summary>
/// Classifies RouterOS commands as read or write and enforces the read-only guardrail.
/// This is the single chokepoint every write must pass through.
/// </summary>
public sealed class AccessPolicy
{
    // RouterOS "verbs" — the last segment of an API path — that mutate router state.
    private static readonly string[] WriteVerbs =
    {
        "add", "set", "remove", "move", "enable", "disable", "unset",
        "reset", "reset-configuration", "comment", "edit",
    };

    /// <summary>
    /// True when the command mutates router state (its trailing path segment is a write verb).
    /// Examples: <c>/ip/address/add</c> (write), <c>/ip/address/print</c> (read).
    /// </summary>
    public bool IsWriteCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

        var lastSegment = command.Trim()
            .Trim('/')
            .Split('/')
            .LastOrDefault()?
            .ToLowerInvariant();

        return lastSegment is not null && WriteVerbs.Contains(lastSegment);
    }

    /// <summary>
    /// Throws <see cref="AccessDeniedException"/> when <paramref name="command"/> is a write but the
    /// effective policy (global AND per-router) is read-only.
    /// </summary>
    public void EnsureAllowed(string command, bool effectiveReadOnly)
    {
        if (effectiveReadOnly && IsWriteCommand(command))
        {
            throw new AccessDeniedException(
                $"Refused write command '{command}': this server (or router) is in read-only mode. " +
                "Set Tik4Mcp:ReadOnly=false (and the router's ReadOnly=false) to allow writes.");
        }
    }
}

/// <summary>Raised when a command is blocked by the <see cref="AccessPolicy"/>.</summary>
public sealed class AccessDeniedException : Exception
{
    public AccessDeniedException(string message) : base(message) { }
}
