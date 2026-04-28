namespace TILSOFTAI.Orchestration.Semantic;

public sealed record HardSignalSet
{
    public IReadOnlyList<CodeSignal> Codes { get; init; } = Array.Empty<CodeSignal>();
    public IReadOnlyList<DateSignal> Dates { get; init; } = Array.Empty<DateSignal>();
    public IReadOnlyList<NumberSignal> Numbers { get; init; } = Array.Empty<NumberSignal>();
    public IReadOnlyList<string> BusinessKeywords { get; init; } = Array.Empty<string>();
    public string? DetectedLanguage { get; init; }
}

public sealed record CodeSignal
{
    public required string Text { get; init; }
    public string? PossibleType { get; init; }
}

public sealed record DateSignal
{
    public required string Text { get; init; }
    public string? Kind { get; init; }
}

public sealed record NumberSignal
{
    public required string Text { get; init; }
    public decimal? Value { get; init; }
    public string? Unit { get; init; }
}

public sealed record CapabilityRetrievalOptions
{
    public int MaxDomainsPerRequest { get; init; } = 2;
    public int MaxToolsPerDomain { get; init; } = 6;
    public int MaxTotalTools { get; init; } = 12;

    public static CapabilityRetrievalOptions Default { get; } = new();
}

public sealed record DomainCandidate
{
    public required string Domain { get; init; }
    public double Score { get; init; }
}

public sealed record CapabilityCandidate
{
    public required CapabilitySemanticMetadata Metadata { get; init; }
    public double Score { get; init; }
}

public sealed record CapabilityRetrievalResult
{
    public required IReadOnlyList<DomainCandidate> Domains { get; init; }
    public required IReadOnlyList<CapabilityCandidate> Capabilities { get; init; }
    public required IReadOnlyList<EntityCandidate> EntityCandidates { get; init; }
    public required IReadOnlyList<KnowledgeChunk> ContextChunks { get; init; }
}
