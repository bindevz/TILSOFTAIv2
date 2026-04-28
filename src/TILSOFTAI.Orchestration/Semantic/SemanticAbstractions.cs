using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Semantic;

public interface ISemanticKnowledgeRepository
{
    Task<IReadOnlyList<KnowledgeChunk>> SearchChunksAsync(
        SemanticSearchRequest request,
        CancellationToken cancellationToken);
}

public interface ICapabilityMetadataRepository
{
    Task<CapabilitySemanticMetadata?> GetCapabilityMetadataAsync(
        string capabilityKey,
        string locale,
        CancellationToken cancellationToken);
}

public interface IEntityAliasRepository
{
    Task<IReadOnlyList<EntityCandidate>> SearchAliasesAsync(
        EntityAliasSearchRequest request,
        CancellationToken cancellationToken);
}

public interface IToolRoutingTraceStore
{
    Task SaveAsync(ToolRoutingTrace trace, CancellationToken cancellationToken);
}

public interface IHardSignalExtractor
{
    HardSignalSet Extract(
        string message,
        string locale,
        TilsoftExecutionContext context);
}

public interface ISemanticCapabilityRetriever
{
    Task<CapabilityRetrievalResult> RetrieveAsync(
        string userMessage,
        HardSignalSet hardSignals,
        TilsoftExecutionContext context,
        string locale,
        CapabilityRetrievalOptions options,
        CancellationToken cancellationToken);
}
