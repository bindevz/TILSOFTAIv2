using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Semantic;
using Xunit;

namespace TILSOFTAI.Tests.AiRouting;

public sealed class SemanticCapabilityRetrieverTests
{
    [Fact]
    public void DomainGate_ShouldSelectHighestScoringDomainsWithinLimit()
    {
        var gate = new DomainGate();

        var domains = gate.SelectDomains(
            [
                Chunk("model.overview.by-code", "model", 0.72),
                Chunk("model.materials.by-code", "model", 0.96),
                Chunk("warehouse.stock.available", "warehouse", 0.83),
                Chunk("sales.order.status", "sales", 0.91)
            ],
            new CapabilityRetrievalOptions { MaxDomainsPerRequest = 2, AllowedDomains = ModelOnlyDomains() });

        domains.Select(domain => domain.Domain).Should().Equal("model");
        domains.Select(domain => domain.Score).Should().Equal(0.96);
    }

    [Fact]
    public void DomainGate_ShouldFailClosedToModelOnlyEvenWhenNonModelDomainsAreConfigured()
    {
        var gate = new DomainGate();

        var domains = gate.SelectDomains(
            [
                Chunk("sales.order.status", "sales", 0.99),
                Chunk("warehouse.stock.available", "warehouse", 0.98),
                Chunk("model.overview.by-code", "product_model", 0.70)
            ],
            new CapabilityRetrievalOptions
            {
                MaxDomainsPerRequest = 3,
                AllowedDomains = new HashSet<string>(["sales", "warehouse"], StringComparer.OrdinalIgnoreCase)
            });

        domains.Select(domain => domain.Domain).Should().Equal("model");
        domains.Select(domain => domain.Score).Should().Equal(0.70);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldEnforceDomainAndToolLimits()
    {
        var knowledgeRepository = new StubSemanticKnowledgeRepository(
            [
                Chunk("model.overview.by-code", "model", 0.95),
                Chunk("model.materials.by-code", "model", 0.91),
                Chunk("model.pieces.by-code", "model", 0.93),
                Chunk("sales.order.status", "sales", 0.90)
            ]);
        var metadataRepository = new StubCapabilityMetadataRepository(
            Metadata("model.overview.by-code", "model"),
            Metadata("model.materials.by-code", "model"),
            Metadata("model.pieces.by-code", "model"),
            Metadata("sales.order.status", "sales"));
        var retriever = CreateRetriever(knowledgeRepository, metadataRepository);

        var result = await retriever.RetrieveAsync(
            "show me stock and balances",
            new HardSignalSet(),
            new TilsoftExecutionContext { TenantId = "tenant-32" },
            "en-US",
            new CapabilityRetrievalOptions
            {
                MaxDomainsPerRequest = 2,
                MaxToolsPerDomain = 1,
                MaxTotalTools = 2
            },
            CancellationToken.None);

        result.Domains.Select(domain => domain.Domain).Should().Equal("model");
        result.Capabilities.Should().ContainSingle();
        result.Capabilities.Select(candidate => candidate.Metadata.Domain)
            .Should()
            .BeSubsetOf(["model"]);
        result.Capabilities
            .GroupBy(candidate => candidate.Metadata.Domain)
            .Should()
            .OnlyContain(group => group.Count() <= 1);
        knowledgeRepository.Requests.Should().HaveCount(2);
        knowledgeRepository.Requests[1].Domains.Should().Equal("model");
    }

    [Fact]
    public async Task RetrieveAsync_ShouldNeverReturnNonModelCapabilitiesEvenIfConfigured()
    {
        var knowledgeRepository = new StubSemanticKnowledgeRepository(
            [
                Chunk("sales.order.status", "sales", 0.99),
                Chunk("warehouse.stock.available", "warehouse", 0.98),
                Chunk("model.overview.by-code", "product_model", 0.70),
                Chunk("model.materials.by-code", "model", 0.69)
            ]);
        var metadataRepository = new StubCapabilityMetadataRepository(
            Metadata("sales.order.status", "sales"),
            Metadata("warehouse.stock.available", "warehouse"),
            Metadata("model.overview.by-code", "product_model"),
            Metadata("model.materials.by-code", "model"));
        var options = Options.Create(new AiRoutingOptions
        {
            AllowedDomains = ["sales", "warehouse"],
            MaxCandidateDomains = 3,
            MaxCandidateToolsPerDomain = 6,
            MaxTotalCandidateTools = 6,
            MaxCandidateTools = 6
        });
        var retriever = new SemanticCapabilityRetriever(
            knowledgeRepository,
            metadataRepository,
            new StubEntityAliasRepository(),
            new DomainGate(),
            options);

        var result = await retriever.RetrieveAsync(
            "show stock and sales",
            new HardSignalSet(),
            new TilsoftExecutionContext { TenantId = "tenant-35" },
            "en-US",
            new CapabilityRetrievalOptions
            {
                MaxDomainsPerRequest = 3,
                MaxToolsPerDomain = 6,
                MaxTotalTools = 6
            },
            CancellationToken.None);

        result.Domains.Select(domain => domain.Domain).Should().Equal("model");
        result.Capabilities.Select(candidate => DomainGate.NormalizeDomain(candidate.Metadata.Domain))
            .Should()
            .OnlyContain(domain => domain == "model");
        result.Capabilities.Select(candidate => candidate.Metadata.CapabilityKey)
            .Should()
            .Equal("model.materials.by-code");
        knowledgeRepository.Requests.Should().HaveCount(2);
        knowledgeRepository.Requests[1].Domains.Should().Equal("model");
    }

    [Fact]
    public async Task RetrieveAsync_ShouldNotRunUnscopedCapabilitySearchWhenDomainGateFindsNoDomains()
    {
        var knowledgeRepository = new StubSemanticKnowledgeRepository([]);
        var retriever = CreateRetriever(
            knowledgeRepository,
            new StubCapabilityMetadataRepository());

        var result = await retriever.RetrieveAsync(
            "hello there",
            new HardSignalSet(),
            new TilsoftExecutionContext { TenantId = "tenant-32" },
            "en-US",
            CapabilityRetrievalOptions.Default,
            CancellationToken.None);

        result.Domains.Should().BeEmpty();
        result.Capabilities.Should().BeEmpty();
        result.ContextChunks.Should().BeEmpty();
        knowledgeRepository.Requests.Should().ContainSingle();
        knowledgeRepository.Requests[0].Domains.Should().BeEmpty();
    }

    [Fact]
    public async Task CandidateSelector_ShouldUseRetrieverAndEnforceConfiguredTotalToolLimit()
    {
        var knowledgeRepository = new StubSemanticKnowledgeRepository(
            [
                Chunk("model.overview.by-code", "model", 0.95),
                Chunk("model.materials.by-code", "model", 0.91),
                Chunk("model.pieces.by-code", "model", 0.93),
                Chunk("sales.order.status", "sales", 0.90)
            ]);
        var metadataRepository = new StubCapabilityMetadataRepository(
            Metadata("model.overview.by-code", "model"),
            Metadata("model.materials.by-code", "model"),
            Metadata("model.pieces.by-code", "model"),
            Metadata("sales.order.status", "sales"));
        var options = Options.Create(new AiRoutingOptions
        {
            AllowedDomains = ["model"],
            MaxCandidateDomains = 1,
            MaxCandidateToolsPerDomain = 2,
            MaxTotalCandidateTools = 2,
            MaxCandidateTools = 2
        });
        var retriever = new SemanticCapabilityRetriever(
            knowledgeRepository,
            metadataRepository,
            new StubEntityAliasRepository(),
            new DomainGate(),
            options);
        var selector = new SemanticCapabilityCandidateSelector(
            retriever,
            options,
            NullLogger<SemanticCapabilityCandidateSelector>.Instance);

        var candidates = await selector.SelectAsync(
            "show me stock and balances",
            new HardSignalSet(),
            new TilsoftExecutionContext { TenantId = "tenant-33" },
            "en-US",
            CancellationToken.None);

        candidates.Should().HaveCount(2);
        candidates.Select(candidate => candidate.Metadata.CapabilityKey)
            .Should()
            .Equal("model.overview.by-code", "model.pieces.by-code");
        knowledgeRepository.Requests.Should().HaveCount(2);
        knowledgeRepository.Requests[1].Domains.Should().Equal("model");
    }

    private static SemanticCapabilityRetriever CreateRetriever(
        StubSemanticKnowledgeRepository knowledgeRepository,
        StubCapabilityMetadataRepository metadataRepository) =>
        new(
            knowledgeRepository,
            metadataRepository,
            new StubEntityAliasRepository(),
            new DomainGate(),
            Options.Create(new AiRoutingOptions
            {
            MaxCandidateDomains = 2,
            MaxCandidateToolsPerDomain = 1,
            MaxTotalCandidateTools = 2,
            MaxCandidateTools = 2
        }));

    private static IReadOnlySet<string> ModelOnlyDomains() =>
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "model" };

    private static KnowledgeChunk Chunk(string capabilityKey, string? domain, double score) => new()
    {
        ChunkType = "capability",
        ObjectKey = capabilityKey,
        Domain = domain,
        ContentText = capabilityKey,
        Score = score
    };

    private static CapabilitySemanticMetadata Metadata(string key, string domain) => new()
    {
        CapabilityKey = key,
        Domain = domain,
        FunctionName = key.Replace('.', '_'),
        AdapterType = "sql",
        Operation = "read",
        ExecutionMode = "read"
    };

    private sealed class StubSemanticKnowledgeRepository : ISemanticKnowledgeRepository
    {
        private readonly IReadOnlyList<KnowledgeChunk> _chunks;

        public StubSemanticKnowledgeRepository(IReadOnlyList<KnowledgeChunk> chunks)
        {
            _chunks = chunks;
        }

        public List<SemanticSearchRequest> Requests { get; } = [];

        public Task<IReadOnlyList<KnowledgeChunk>> SearchChunksAsync(
            SemanticSearchRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (request.Domains.Count == 0)
            {
                return Task.FromResult(_chunks);
            }

            var domainSet = request.Domains.ToHashSet(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<KnowledgeChunk> scopedChunks = _chunks
                .Where(chunk => chunk.Domain is not null && domainSet.Contains(chunk.Domain))
                .ToArray();

            return Task.FromResult(scopedChunks);
        }
    }

    private sealed class StubCapabilityMetadataRepository : ICapabilityMetadataRepository
    {
        private readonly IReadOnlyDictionary<string, CapabilitySemanticMetadata> _items;

        public StubCapabilityMetadataRepository(params CapabilitySemanticMetadata[] items)
        {
            _items = items.ToDictionary(item => item.CapabilityKey, StringComparer.OrdinalIgnoreCase);
        }

        public Task<CapabilitySemanticMetadata?> GetCapabilityMetadataAsync(
            string capabilityKey,
            string locale,
            CancellationToken cancellationToken)
        {
            _items.TryGetValue(capabilityKey, out var metadata);
            return Task.FromResult(metadata);
        }
    }

    private sealed class StubEntityAliasRepository : IEntityAliasRepository
    {
        public Task<IReadOnlyList<EntityCandidate>> SearchAliasesAsync(
            EntityAliasSearchRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EntityCandidate>>(Array.Empty<EntityCandidate>());
    }
}
