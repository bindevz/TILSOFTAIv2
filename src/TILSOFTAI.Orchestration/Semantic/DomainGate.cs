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

        var allowedDomains = BuildRuntimeAllowedDomainSet(options.AllowedDomains);

        return chunks
            .Where(chunk => !string.IsNullOrWhiteSpace(chunk.Domain))
            .Select(chunk => new
            {
                Domain = NormalizeDomain(chunk.Domain!),
                chunk.Score
            })
            .Where(chunk => allowedDomains.Contains(chunk.Domain))
            .GroupBy(chunk => chunk.Domain, StringComparer.OrdinalIgnoreCase)
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

    public static bool IsRuntimeAllowedDomain(string? domain) =>
        string.Equals(NormalizeDomain(domain ?? string.Empty), "model", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlySet<string> BuildRuntimeAllowedDomainSet(IEnumerable<string>? domains)
    {
        var normalized = (domains ?? Array.Empty<string>())
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Select(NormalizeDomain)
            .Where(IsRuntimeAllowedDomain)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0
            ? DefaultAllowedDomains
            : new HashSet<string>(normalized, StringComparer.OrdinalIgnoreCase);
    }
}
