namespace TILSOFTAI.Orchestration.Semantic;

public sealed record SemanticSearchRequest
{
    public required string Text { get; init; }
    public string? TenantId { get; init; }
    public string? Locale { get; init; }
    public IReadOnlyList<string> Domains { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> HardSignals { get; init; } = Array.Empty<string>();
    public int TopK { get; init; } = 12;
}

public sealed record KnowledgeChunk
{
    public long ChunkId { get; init; }
    public string? TenantId { get; init; }
    public required string ChunkType { get; init; }
    public required string ObjectKey { get; init; }
    public string? Domain { get; init; }
    public string? Locale { get; init; }
    public string? Title { get; init; }
    public required string ContentText { get; init; }
    public string? Metadata { get; init; }
    public double Score { get; init; }
}

public sealed record CapabilitySemanticMetadata
{
    public required string CapabilityKey { get; init; }
    public required string Domain { get; init; }
    public string? BusinessArea { get; init; }
    public required string FunctionName { get; init; }
    public required string AdapterType { get; init; }
    public required string Operation { get; init; }
    public string? StoredProcedure { get; init; }
    public required string ExecutionMode { get; init; }
    public string? ArgumentContract { get; init; }
    public string? ResultSchema { get; init; }
    public string? AnswerPolicy { get; init; }
    public string? SensitivityPolicy { get; init; }
    public string? RequiredRoles { get; init; }
    public string? AllowedTenants { get; init; }
    public string? SubCapabilities { get; init; }
    public string? CompositionPolicy { get; init; }
    public bool AllowMultiCall { get; init; }
    public int VersionNo { get; init; }
    public CapabilityTextMetadata? Text { get; init; }
    public IReadOnlyList<CapabilityArgumentMetadata> Arguments { get; init; } = Array.Empty<CapabilityArgumentMetadata>();
    public IReadOnlyList<CapabilityExampleMetadata> Examples { get; init; } = Array.Empty<CapabilityExampleMetadata>();
}

public interface ICapabilityCandidateSelector
{
    Task<IReadOnlyList<CapabilityCandidate>> SelectAsync(
        string userMessage,
        HardSignalSet hardSignals,
        TILSOFTAI.Domain.ExecutionContext.TilsoftExecutionContext context,
        string locale,
        CancellationToken cancellationToken);
}

public sealed record CapabilityTextMetadata
{
    public required string Locale { get; init; }
    public string? ShortName { get; init; }
    public required string Description { get; init; }
    public string? Aliases { get; init; }
    public string? UseWhen { get; init; }
    public string? DoNotUseWhen { get; init; }
    public string? BusinessNotes { get; init; }
}

public sealed record CapabilityArgumentMetadata
{
    public required string ArgumentName { get; init; }
    public required string ProcParameterName { get; init; }
    public required string DataType { get; init; }
    public bool IsRequired { get; init; }
    public string? DefaultSource { get; init; }
    public string? ValidationRule { get; init; }
    public string? ClarificationPolicy { get; init; }
    public int DisplayOrder { get; init; }
    public CapabilityArgumentTextMetadata? Text { get; init; }
}

public sealed record CapabilityArgumentTextMetadata
{
    public required string Locale { get; init; }
    public required string Description { get; init; }
    public string? Aliases { get; init; }
    public string? Examples { get; init; }
    public string? ClarificationQuestion { get; init; }
}

public sealed record CapabilityExampleMetadata
{
    public required string Locale { get; init; }
    public required string Utterance { get; init; }
    public string? ArgumentsJson { get; init; }
    public int SortOrder { get; init; }
}

public sealed record EntityAliasSearchRequest
{
    public required string Text { get; init; }
    public string? TenantId { get; init; }
    public string? Locale { get; init; }
    public IReadOnlyList<string> EntityTypes { get; init; } = Array.Empty<string>();
    public int TopK { get; init; } = 10;
}

public sealed record EntityCandidate
{
    public long EntityAliasId { get; init; }
    public string? TenantId { get; init; }
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public string? CanonicalCode { get; init; }
    public string? CanonicalName { get; init; }
    public required string AliasText { get; init; }
    public required string AliasNormalized { get; init; }
    public string? Locale { get; init; }
    public string? Metadata { get; init; }
    public double Score { get; init; }
}

public sealed record ToolRoutingTrace
{
    public Guid CorrelationId { get; init; }
    public required string TenantId { get; init; }
    public required string UserId { get; init; }
    public string? ConversationId { get; init; }
    public string? Locale { get; init; }
    public byte[]? UserMessageHash { get; init; }
    public string? UserMessageRedacted { get; init; }
    public string? CandidateDomainsJson { get; init; }
    public string? CandidateToolsJson { get; init; }
    public string? HardSignalsJson { get; init; }
    public string? AdvertisedFunctionToolsJson { get; init; }
    public string? SelectedTool { get; init; }
    public string? SelectedFunction { get; init; }
    public string? SelectedCapabilityKey { get; init; }
    public string? StoredProcedure { get; init; }
    public int? CandidateDomainCount { get; init; }
    public int? CandidateCapabilityCount { get; init; }
    public int? AdvertisedToolCount { get; init; }
    public string? ArgumentsJson { get; init; }
    public string? ArgumentsBeforeNormalizationJson { get; init; }
    public string? ArgumentsAfterNormalizationJson { get; init; }
    public string? ValidationResultJson { get; init; }
    public string? AdapterType { get; init; }
    public int? RowCount { get; init; }
    public string? AnswerMode { get; init; }
    public int? LatencyMs { get; init; }
    public string? LatencyByStageJson { get; init; }
    public string? ModelProvider { get; init; }
    public string? ModelName { get; init; }
    public string? AllowedDomainsJson { get; init; }
    public bool FallbackUsed { get; init; }
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
}
