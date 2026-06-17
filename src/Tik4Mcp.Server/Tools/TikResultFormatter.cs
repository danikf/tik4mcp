using System.Collections.Generic;
using System.Text.Json;
using tik4net;

namespace Tik4Mcp.Server.Tools;

/// <summary>Shared formatting of RouterOS sentence results into compact JSON strings for the AI.</summary>
internal static class TikResultFormatter
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>
    /// Serializes <c>!re</c> records to a JSON array; returns a short status string for empty results
    /// and a "TRAP …" string when the router rejected the command.
    /// </summary>
    public static string Format(IEnumerable<ITikSentence> sentences)
    {
        var records = new List<Dictionary<string, string>>();

        foreach (var sentence in sentences)
        {
            switch (sentence)
            {
                case ITikReSentence re:
                    records.Add(new Dictionary<string, string>(re.Words));
                    break;
                case ITikTrapSentence trap:
                    return $"TRAP [{trap.CategoryCode}]: {trap.Message}";
            }
        }

        return records.Count == 0
            ? "OK (no data returned)"
            : JsonSerializer.Serialize(records, Indented);
    }

    public static string ToJson(object value) => JsonSerializer.Serialize(value, Indented);
}
