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
                Chunk("warehouse.inventory.by-item", "warehouse", 0.72),
                Chunk("accounting.ar.balance", "accounting", 0.96),
                Chunk("warehouse.stock.available", "warehouse", 0.83),
                Chunk("sales.order.status", "sales", 0.91)
            ],
            new CapabilityRetrievalOptions { MaxDomainsPerRequest = 2 });

        domains.Select(domain => domain.Domain).Should().Equal("accounting", "sales");
        domains.Select(domain => domain.Score).Should().Equal(0.96, 0.91);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldEnforceDomainAndToolLimits()
    {
        var knowledgeRepository = new StubSemanticKnowledgeRepository(
            [
                Chunk("warehouse.inventory.by-item", "warehouse", 0.95),
                Chunk("warehouse.stock.available", "warehouse", 0.91),
                Chunk("accounting.ar.balance", "accounting", 0.93),
                Chunk("sales.order.status", "sales", 0.90)
            ]);
        var metadataRepository = new StubCapabilityMetadataRepository(
            Metadata("warehouse.inventory.by-item", "warehouse"),
            Metadata("warehouse.stock.available", "warehouse"),
            Metadata("accounting.ar.balance", "accounting"),
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

        result.Domains.Select(domain => domain.Domain).Should().Equal("warehouse", "accounting");
        result.Capabilities.Should().HaveCount(2);
        result.Capabilities.Select(candidate => candidate.Metadata.Domain)
            .Should()
            .BeSubsetOf(["warehouse", "accounting"]);
        result.Capabilities
            .GroupBy(candidate => candidate.Metadata.Domain)
            .Should()
            .OnlyContain(group => group.Count() <= 1);
        knowledgeRepository.Requests.Should().HaveCount(2);
        knowledgeRepository.Requests[1].Domains.Should().Equal("warehouse", "accounting");
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
                Chunk("warehouse.inventory.by-item", "warehouse", 0.95),
                Chunk("warehouse.stock.available", "warehouse", 0.91),
                Chunk("accounting.ar.balance", "accounting", 0.93),
                Chunk("sales.order.status", "sales", 0.90)
            ]);
        var metadataRepository = new StubCapabilityMetadataRepository(
            Metadata("warehouse.inventory.by-item", "warehouse"),
            Metadata("warehouse.stock.available", "warehouse"),
            Metadata("accounting.ar.balance", "accounting"),
            Metadata("sales.order.status", "sales"));
        var options = Options.Create(new AiRoutingOptions
        {
            MaxCandidateDomains = 2,
            MaxCandidateToolsPerDomain = 2,
            MaxTotalCandidateTools = 2
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
            .Equal("warehouse.inventory.by-item", "accounting.ar.balance");
        knowledgeRepository.Requests.Should().HaveCount(2);
        knowledgeRepository.Requests[1].Domains.Should().Equal("warehouse", "accounting");
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
                MaxTotalCandidateTools = 2
            }));

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
