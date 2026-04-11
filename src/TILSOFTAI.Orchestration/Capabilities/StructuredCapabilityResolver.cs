using Microsoft.Extensions.Logging;

namespace TILSOFTAI.Orchestration.Capabilities;

/// <summary>
/// Sprint 5: Default structured capability resolver.
/// Resolution priority:
///   1. Exact CapabilityKey match from hint
///   2. Subject keyword matching against capability key segments
///   3. Null (no match — agent should fall back to bridge)
///
/// Used by both WarehouseAgent and AccountingAgent.
/// </summary>
public sealed class StructuredCapabilityResolver : ICapabilityResolver
{
    private readonly ILogger<StructuredCapabilityResolver> _logger;

    public StructuredCapabilityResolver(ILogger<StructuredCapabilityResolver> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public CapabilityDescriptor? Resolve(CapabilityRequestHint hint, IReadOnlyList<CapabilityDescriptor> candidates)
    {
        if (hint is null || candidates is null || candidates.Count == 0)
        {
            return null;
        }

        // Priority 1: Exact capability key match
        if (!string.IsNullOrWhiteSpace(hint.CapabilityKey))
        {
            var exactMatch = candidates.FirstOrDefault(c =>
                string.Equals(c.CapabilityKey, hint.CapabilityKey, StringComparison.OrdinalIgnoreCase));

            if (exactMatch is not null)
            {
                _logger.LogDebug(
                    "CapabilityResolved | Strategy: exact_key | Key: {CapabilityKey}",
                    exactMatch.CapabilityKey);
                return exactMatch;
            }
        }

        // Priority 2: Subject keyword matching against capability key segments
        if (hint.SubjectKeywords.Count > 0)
        {
            var bestMatch = (CapabilityDescriptor?)null;
            var bestScore = 0;

            foreach (var cap in candidates)
            {
                var keyParts = cap.CapabilityKey
                    .Split('.', StringSplitOptions.RemoveEmptyEntries)
                    .Skip(1) // skip the domain prefix (e.g. "warehouse", "accounting")
                    .SelectMany(part => part.Split('-', StringSplitOptions.RemoveEmptyEntries))
                    .ToList();

                var score = 0;
                foreach (var keyword in hint.SubjectKeywords)
                {
                    if (keyParts.Any(p => string.Equals(p, keyword, StringComparison.OrdinalIgnoreCase)))
                    {
                        score++;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = cap;
                }
            }

            if (bestMatch is not null && bestScore > 0)
            {
                _logger.LogDebug(
                    "CapabilityResolved | Strategy: keyword_match | Key: {CapabilityKey} | Score: {Score}/{Total}",
                    bestMatch.CapabilityKey, bestScore, hint.SubjectKeywords.Count);
                return bestMatch;
            }
        }

        _logger.LogDebug(
            "CapabilityNotResolved | Domain: {Domain} | HintKey: {HintKey} | Keywords: [{Keywords}] | CandidateCount: {Count}",
            hint.Domain ?? "none",
            hint.CapabilityKey ?? "none",
            string.Join(", ", hint.SubjectKeywords),
            candidates.Count);

        return null;
    }
}
