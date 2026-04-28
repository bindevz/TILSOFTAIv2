using System.Text.Json;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Semantic;

public sealed class SemanticCapabilityRetriever : ISemanticCapabilityRetriever
{
    private readonly ISemanticKnowledgeRepository _knowledgeRepository;
    private readonly ICapabilityMetadataRepository _capabilityMetadataRepository;
    private readonly IEntityAliasRepository _entityAliasRepository;
    private readonly AiRoutingOptions _options;

    public SemanticCapabilityRetriever(
        ISemanticKnowledgeRepository knowledgeRepository,
        ICapabilityMetadataRepository capabilityMetadataRepository,
        IEntityAliasRepository entityAliasRepository,
        IOptions<AiRoutingOptions> options)
    {
        _knowledgeRepository = knowledgeRepository ?? throw new ArgumentNullException(nameof(knowledgeRepository));
        _capabilityMetadataRepository = capabilityMetadataRepository ?? throw new ArgumentNullException(nameof(capabilityMetadataRepository));
        _entityAliasRepository = entityAliasRepository ?? throw new ArgumentNullException(nameof(entityAliasRepository));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<CapabilityRetrievalResult> RetrieveAsync(
        string userMessage,
        HardSignalSet hardSignals,
        TilsoftExecutionContext context,
        string locale,
        CapabilityRetrievalOptions options,
        CancellationToken cancellationToken)
    {
        var effectiveOptions = new CapabilityRetrievalOptions
        {
            MaxDomainsPerRequest = _options.MaxCandidateDomains,
            MaxToolsPerDomain = _options.MaxCandidateToolsPerDomain,
            MaxTotalTools = _options.MaxTotalCandidateTools
        };

        var signalText = hardSignals.BusinessKeywords
            .Concat(hardSignals.Codes.Select(code => code.Text))
            .Concat(hardSignals.Dates.Select(date => date.Text))
            .ToArray();

        var initialChunks = await _knowledgeRepository.SearchChunksAsync(
            new SemanticSearchRequest
            {
                Text = userMessage,
                TenantId = context.TenantId,
                Locale = locale,
                HardSignals = signalText,
                TopK = Math.Max(effectiveOptions.MaxTotalTools * 2, 12)
            },
            cancellationToken);

        var domains = initialChunks
            .Where(chunk => !string.IsNullOrWhiteSpace(chunk.Domain))
            .GroupBy(chunk => chunk.Domain!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DomainCandidate
            {
                Domain = group.Key,
                Score = group.Max(chunk => chunk.Score)
            })
            .OrderByDescending(domain => domain.Score)
            .ThenBy(domain => domain.Domain, StringComparer.OrdinalIgnoreCase)
            .Take(effectiveOptions.MaxDomainsPerRequest)
            .ToArray();

        var domainNames = domains.Select(domain => domain.Domain).ToArray();
        var contextChunks = await _knowledgeRepository.SearchChunksAsync(
            new SemanticSearchRequest
            {
                Text = userMessage,
                TenantId = context.TenantId,
                Locale = locale,
                Domains = domainNames,
                HardSignals = signalText,
                TopK = Math.Max(effectiveOptions.MaxTotalTools * 2, 12)
            },
            cancellationToken);

        var entityCandidates = await _entityAliasRepository.SearchAliasesAsync(
            new EntityAliasSearchRequest
            {
                Text = userMessage,
                TenantId = context.TenantId,
                Locale = locale,
                TopK = 10
            },
            cancellationToken);

        var capabilityKeys = contextChunks
            .Select(TryReadCapabilityKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var candidates = new List<CapabilityCandidate>();
        foreach (var capabilityKey in capabilityKeys)
        {
            var metadata = await _capabilityMetadataRepository.GetCapabilityMetadataAsync(
                capabilityKey!,
                locale,
                cancellationToken);

            if (metadata is null || !domainNames.Contains(metadata.Domain, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var chunkScore = contextChunks
                .Where(chunk => string.Equals(TryReadCapabilityKey(chunk), capabilityKey, StringComparison.OrdinalIgnoreCase))
                .Select(chunk => chunk.Score)
                .DefaultIfEmpty(0)
                .Max();

            candidates.Add(new CapabilityCandidate
            {
                Metadata = metadata,
                Score = chunkScore
            });
        }

        var cappedCapabilities = candidates
            .GroupBy(candidate => candidate.Metadata.Domain, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Metadata.CapabilityKey, StringComparer.OrdinalIgnoreCase)
                .Take(effectiveOptions.MaxToolsPerDomain))
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Metadata.CapabilityKey, StringComparer.OrdinalIgnoreCase)
            .Take(effectiveOptions.MaxTotalTools)
            .ToArray();

        return new CapabilityRetrievalResult
        {
            Domains = domains,
            Capabilities = cappedCapabilities,
            EntityCandidates = entityCandidates,
            ContextChunks = contextChunks
        };
    }

    private static string? TryReadCapabilityKey(KnowledgeChunk chunk)
    {
        if (string.Equals(chunk.ChunkType, "capability", StringComparison.OrdinalIgnoreCase))
        {
            return chunk.ObjectKey;
        }

        if (string.IsNullOrWhiteSpace(chunk.Metadata))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(chunk.Metadata);
            return document.RootElement.TryGetProperty("capabilityKey", out var property)
                ? property.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
