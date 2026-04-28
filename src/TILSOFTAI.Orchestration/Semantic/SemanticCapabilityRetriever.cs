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
    private readonly IDomainGate _domainGate;
    private readonly AiRoutingOptions _options;

    public SemanticCapabilityRetriever(
        ISemanticKnowledgeRepository knowledgeRepository,
        ICapabilityMetadataRepository capabilityMetadataRepository,
        IEntityAliasRepository entityAliasRepository,
        IDomainGate domainGate,
        IOptions<AiRoutingOptions> options)
    {
        _knowledgeRepository = knowledgeRepository ?? throw new ArgumentNullException(nameof(knowledgeRepository));
        _capabilityMetadataRepository = capabilityMetadataRepository ?? throw new ArgumentNullException(nameof(capabilityMetadataRepository));
        _entityAliasRepository = entityAliasRepository ?? throw new ArgumentNullException(nameof(entityAliasRepository));
        _domainGate = domainGate ?? throw new ArgumentNullException(nameof(domainGate));
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
        var effectiveOptions = ResolveOptions(options);

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

        var domains = _domainGate.SelectDomains(initialChunks, effectiveOptions);

        var allowedDomains = effectiveOptions.AllowedDomains.Count == 0
            ? DomainGate.DefaultAllowedDomains
            : effectiveOptions.AllowedDomains;
        var domainNames = domains
            .Select(domain => DomainGate.NormalizeDomain(domain.Domain))
            .Where(allowedDomains.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (domainNames.Length == 0)
        {
            var emptyEntityCandidates = await SearchEntityCandidatesAsync(
                userMessage,
                context,
                locale,
                cancellationToken);

            return new CapabilityRetrievalResult
            {
                Domains = domains,
                Capabilities = Array.Empty<CapabilityCandidate>(),
                EntityCandidates = emptyEntityCandidates,
                ContextChunks = Array.Empty<KnowledgeChunk>()
            };
        }

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

        var entityCandidates = await SearchEntityCandidatesAsync(
            userMessage,
            context,
            locale,
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

            var metadataDomain = DomainGate.NormalizeDomain(metadata?.Domain ?? string.Empty);
            if (metadata is null || !domainNames.Contains(metadataDomain, StringComparer.OrdinalIgnoreCase))
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

    private CapabilityRetrievalOptions ResolveOptions(CapabilityRetrievalOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new CapabilityRetrievalOptions
        {
            AllowedDomains = BuildAllowedDomainSet(_options.AllowedDomains),
            MaxDomainsPerRequest = EffectiveLimit(options.MaxDomainsPerRequest, _options.MaxCandidateDomains),
            MaxToolsPerDomain = EffectiveLimit(options.MaxToolsPerDomain, _options.MaxCandidateToolsPerDomain),
            MaxTotalTools = EffectiveLimit(options.MaxTotalTools, EffectiveMaxCandidateTools())
        };
    }

    private int EffectiveMaxCandidateTools()
    {
        var maxCandidateTools = _options.MaxCandidateTools > 0
            ? _options.MaxCandidateTools
            : _options.MaxTotalCandidateTools;
        return Math.Max(0, Math.Min(maxCandidateTools, _options.MaxTotalCandidateTools));
    }

    private static IReadOnlySet<string> BuildAllowedDomainSet(IEnumerable<string>? domains)
    {
        var normalized = (domains ?? Array.Empty<string>())
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Select(DomainGate.NormalizeDomain)
            .ToArray();

        return normalized.Length == 0
            ? DomainGate.DefaultAllowedDomains
            : new HashSet<string>(normalized, StringComparer.OrdinalIgnoreCase);
    }

    private static int EffectiveLimit(int requestLimit, int configuredLimit)
    {
        if (configuredLimit <= 0 || requestLimit <= 0)
        {
            return 0;
        }

        return Math.Min(requestLimit, configuredLimit);
    }

    private Task<IReadOnlyList<EntityCandidate>> SearchEntityCandidatesAsync(
        string userMessage,
        TilsoftExecutionContext context,
        string locale,
        CancellationToken cancellationToken) =>
        _entityAliasRepository.SearchAliasesAsync(
            new EntityAliasSearchRequest
            {
                Text = userMessage,
                TenantId = context.TenantId,
                Locale = locale,
                TopK = 10
            },
            cancellationToken);
}
