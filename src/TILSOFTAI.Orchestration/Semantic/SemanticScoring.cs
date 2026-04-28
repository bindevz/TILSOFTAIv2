using System.Globalization;
using System.Text;

namespace TILSOFTAI.Orchestration.Semantic;

public static class SemanticScoring
{
    public static IReadOnlyList<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var normalized = Normalize(text);
        return normalized
            .Split(new[] { ' ', ',', '.', '?', '!', ':', ';', '-', '_', '/', '\\', '(', ')', '[', ']' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static double ScoreText(
        string userText,
        string searchableText,
        string? domain,
        IReadOnlyList<string> requestedDomains,
        IReadOnlyList<string> hardSignals,
        double recentSuccessTraceScore = 0)
    {
        var normalizedUserText = Normalize(userText);
        var normalizedSearchableText = Normalize(searchableText);
        var userTokens = Tokenize(normalizedUserText);

        var keywordScore = userTokens.Count == 0
            ? 0
            : userTokens.Count(token => normalizedSearchableText.Contains(token, StringComparison.OrdinalIgnoreCase)) / (double)userTokens.Count;

        var exactAliasMatch = !string.IsNullOrWhiteSpace(normalizedUserText)
                              && normalizedSearchableText.Contains(normalizedUserText, StringComparison.OrdinalIgnoreCase)
            ? 1
            : 0;

        var hardSignalMatch = hardSignals.Count == 0
            ? 0
            : hardSignals.Count(signal => normalizedSearchableText.Contains(Normalize(signal), StringComparison.OrdinalIgnoreCase)) / (double)hardSignals.Count;

        var domainPrior = !string.IsNullOrWhiteSpace(domain)
                          && requestedDomains.Any(requested => string.Equals(requested, domain, StringComparison.OrdinalIgnoreCase))
            ? 1
            : 0;

        var vectorSimilarity = keywordScore;

        return (0.45 * vectorSimilarity)
               + (0.20 * exactAliasMatch)
               + (0.15 * hardSignalMatch)
               + (0.10 * domainPrior)
               + (0.10 * Math.Clamp(recentSuccessTraceScore, 0, 1));
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var formD = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch == 'đ' ? 'd' : ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
