namespace TILSOFTAI.Supervisor.Classification;

/// <summary>
/// Result of intent classification for an incoming request.
/// Used by SupervisorRuntime to route requests to the best domain agent.
/// </summary>
public sealed class IntentClassification
{
    /// <summary>
    /// Domain hint inferred from classification (e.g., "accounting", "warehouse").
    /// Null when confidence is too low to determine domain.
    /// </summary>
    public string? DomainHint { get; init; }

    /// <summary>
    /// Classification confidence score between 0.0 and 1.0.
    /// </summary>
    public decimal Confidence { get; init; }

    /// <summary>
    /// Inferred intent type (e.g., "query", "write", "chat").
    /// </summary>
    public string IntentType { get; init; } = "chat";

    /// <summary>
    /// Diagnostic reasons explaining the classification decision.
    /// </summary>
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();

    public static IntentClassification Unclassified(string? reason = null) => new()
    {
        DomainHint = null,
        Confidence = 0m,
        IntentType = "chat",
        Reasons = reason is not null ? new[] { reason } : Array.Empty<string>()
    };
}
