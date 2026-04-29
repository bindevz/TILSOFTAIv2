using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Semantic;

public sealed class SemanticCapabilityCandidateSelector : ICapabilityCandidateSelector
{
    private readonly ISemanticCapabilityRetriever _retriever;
    private readonly AiRoutingOptions _options;
    private readonly ILogger<SemanticCapabilityCandidateSelector> _logger;

    public SemanticCapabilityCandidateSelector(
        ISemanticCapabilityRetriever retriever,
        IOptions<AiRoutingOptions> options,
        ILogger<SemanticCapabilityCandidateSelector> logger)
    {
        _retriever = retriever ?? throw new ArgumentNullException(nameof(retriever));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<CapabilityCandidate>> SelectAsync(
        string userMessage,
        HardSignalSet hardSignals,
        TilsoftExecutionContext context,
        string locale,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);
        ArgumentNullException.ThrowIfNull(hardSignals);
        ArgumentNullException.ThrowIfNull(context);

        var allowedDomains = DomainGate.BuildRuntimeAllowedDomainSet(_options.AllowedDomains);
        var options = new CapabilityRetrievalOptions
        {
            AllowedDomains = allowedDomains,
            MaxDomainsPerRequest = Math.Min(
                _options.MaxCandidateDomains,
                allowedDomains.Count),
            MaxToolsPerDomain = Math.Min(_options.MaxCandidateToolsPerDomain, EffectiveMaxCandidateTools()),
            MaxTotalTools = EffectiveMaxCandidateTools()
        };
        var retrieval = await _retriever.RetrieveAsync(
                userMessage,
                hardSignals,
                context,
                locale,
                options,
                cancellationToken)
            .ConfigureAwait(false);

        var selected = retrieval.Capabilities
            .Where(candidate => DomainGate.IsRuntimeAllowedDomain(candidate.Metadata.Domain))
            .Take(Math.Max(0, _options.MaxTotalCandidateTools))
            .ToArray();

        _logger.LogInformation(
            "CapabilityCandidateSelection | SemanticMode: {SemanticMode} | DomainCount: {DomainCount} | CandidateCount: {CandidateCount} | MaxTotalTools: {MaxTotalTools} | Domains: {Domains} | Capabilities: {Capabilities}",
            "TextFallback",
            retrieval.Domains.Count,
            selected.Length,
            EffectiveMaxCandidateTools(),
            string.Join(",", retrieval.Domains.Select(domain => domain.Domain)),
            string.Join(",", selected.Select(candidate => $"{candidate.Metadata.CapabilityKey}:{candidate.Score:0.###}")));

        return selected;
    }

    private int EffectiveMaxCandidateTools()
    {
        var maxCandidateTools = _options.MaxCandidateTools > 0
            ? _options.MaxCandidateTools
            : _options.MaxTotalCandidateTools;
        return Math.Max(0, Math.Min(maxCandidateTools, _options.MaxTotalCandidateTools));
    }

}
