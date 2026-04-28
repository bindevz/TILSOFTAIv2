namespace TILSOFTAI.Orchestration.Semantic;

public sealed class DomainGate : IDomainGate
{
    public static readonly IReadOnlySet<string> DefaultAllowedDomains =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "model" };

    public IReadOnlyList<DomainCandidate> SelectDomains(
        IReadOnlyList<KnowledgeChunk> chunks,
        CapabilityRetrievalOptions options)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxDomainsPerRequest <= 0)
        {
            return Array.Empty<DomainCandidate>();
        }

        var allowedDomains = options.AllowedDomains.Count == 0
            ? DefaultAllowedDomains
            : options.AllowedDomains;

        return chunks
            .Where(chunk => !string.IsNullOrWhiteSpace(chunk.Domain))
            .Where(chunk => allowedDomains.Contains(NormalizeDomain(chunk.Domain!)))
            .GroupBy(chunk => NormalizeDomain(chunk.Domain!), StringComparer.OrdinalIgnoreCase)
            .Select(group => new DomainCandidate
            {
                Domain = group.Key,
                Score = group.Max(chunk => chunk.Score)
            })
            .OrderByDescending(domain => domain.Score)
            .ThenBy(domain => domain.Domain, StringComparer.OrdinalIgnoreCase)
            .Take(options.MaxDomainsPerRequest)
            .ToArray();
    }

    public static string NormalizeDomain(string domain) =>
        string.Equals(domain, "product_model", StringComparison.OrdinalIgnoreCase)
            ? "model"
            : domain.Trim();
}
