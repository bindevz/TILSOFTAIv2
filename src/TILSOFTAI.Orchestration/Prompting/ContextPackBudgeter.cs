namespace TILSOFTAI.Orchestration.Prompting;

public sealed class ContextPackBudgeter
{
    public int EstimateTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var tokens = SplitTokens(text);
        return tokens.Length;
    }

    public string TrimToTokens(string text, int maxTokens)
    {
        if (string.IsNullOrWhiteSpace(text) || maxTokens <= 0)
        {
            return string.Empty;
        }

        var tokens = SplitTokens(text);
        if (tokens.Length <= maxTokens)
        {
            return text;
        }

        var trimmed = string.Join(' ', tokens.Take(maxTokens));
        return $"{trimmed}...";
    }

    // Priority map: lower number = higher priority = removed LAST
    // Critical packs are trimmed (content shortened) rather than dropped entirely
    private static readonly Dictionary<string, int> PackPriority = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tool_catalog"] = 0,        // Highest: LLM needs tool instructions
        ["atomic_catalog"] = 1,      // High: schema for analytics
        ["metadata_dictionary"] = 2  // Can be trimmed
    };

    private const int DefaultPriority = 50;

    public IReadOnlyList<KeyValuePair<string, string>> Budget(IReadOnlyDictionary<string, string> packs)
    {
        if (packs is null || packs.Count == 0)
        {
            return Array.Empty<KeyValuePair<string, string>>();
        }

        // Sort by priority (critical first), then alphabetically
        var ordered = packs
            .OrderBy(pair => PackPriority.GetValueOrDefault(pair.Key, DefaultPriority))
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalChars = ordered.Sum(pair => EstimatePackChars(pair.Key, pair.Value));

        while (totalChars > PromptBudget.MaxContextPacksChars && ordered.Count > 0)
        {
            var lastIndex = ordered.Count - 1;
            var last = ordered[lastIndex];
            var priority = PackPriority.GetValueOrDefault(last.Key, DefaultPriority);

            if (priority <= 1)
            {
                // Critical pack: trim content instead of dropping
                var maxCharsPerPack = Math.Max(200, PromptBudget.MaxContextPacksChars / ordered.Count);
                if (last.Value.Length > maxCharsPerPack)
                {
                    totalChars -= EstimatePackChars(last.Key, last.Value);
                    var trimmed = last.Value[..maxCharsPerPack] + "\n[...truncated]";
                    ordered[lastIndex] = new KeyValuePair<string, string>(last.Key, trimmed);
                    totalChars += EstimatePackChars(last.Key, trimmed);
                }
                break; // Stop removing — all remaining packs are critical
            }

            totalChars -= EstimatePackChars(last.Key, last.Value);
            ordered.RemoveAt(lastIndex);
        }

        return ordered;
    }

    private static int EstimatePackChars(string key, string value)
    {
        var header = $"## {key}{Environment.NewLine}";
        var body = value ?? string.Empty;
        return header.Length + body.Length + Environment.NewLine.Length;
    }

    private static string[] SplitTokens(string text)
    {
        return text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    }
}
