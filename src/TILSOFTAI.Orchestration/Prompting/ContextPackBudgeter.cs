namespace TILSOFTAI.Orchestration.Prompting;

public sealed class ContextPackBudgeter
{
    public IReadOnlyList<KeyValuePair<string, string>> Budget(IReadOnlyDictionary<string, string> packs)
    {
        if (packs is null || packs.Count == 0)
        {
            return Array.Empty<KeyValuePair<string, string>>();
        }

        var ordered = packs
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalChars = ordered.Sum(pair => EstimatePackChars(pair.Key, pair.Value));

        while (totalChars > PromptBudget.MaxContextPacksChars && ordered.Count > 0)
        {
            var lastIndex = ordered.Count - 1;
            var last = ordered[lastIndex];
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
}
