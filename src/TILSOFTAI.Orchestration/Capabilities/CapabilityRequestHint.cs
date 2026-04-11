namespace TILSOFTAI.Orchestration.Capabilities;

/// <summary>
/// Sprint 5: Structured capability request hint.
/// Populated by SupervisorRuntime from classification results and/or explicit request metadata.
/// Used by ICapabilityResolver to select the correct capability without naive string matching.
/// </summary>
public sealed class CapabilityRequestHint
{
    /// <summary>
    /// Explicit capability key (e.g. "accounting.receivables.summary").
    /// When present, the resolver should prefer an exact key match.
    /// </summary>
    public string? CapabilityKey { get; init; }

    /// <summary>
    /// Domain hint from classification (e.g. "warehouse", "accounting").
    /// </summary>
    public string? Domain { get; init; }

    /// <summary>
    /// Inferred operation type (e.g. "query", "summary", "lookup").
    /// </summary>
    public string? Operation { get; init; }

    /// <summary>
    /// Subject keywords extracted from the user input (e.g. ["inventory", "summary"]).
    /// Used as a secondary matching signal when no explicit capability key is provided.
    /// </summary>
    public IReadOnlyList<string> SubjectKeywords { get; init; } = Array.Empty<string>();
}
