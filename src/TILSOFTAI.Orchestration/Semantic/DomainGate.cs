namespace TILSOFTAI.Orchestration.Semantic;

public sealed class DomainGate : IDomainGate
{
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

        return chunks
            .Where(chunk => !string.IsNullOrWhiteSpace(chunk.Domain))
            .GroupBy(chunk => chunk.Domain!, StringComparer.OrdinalIgnoreCase)
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
}
