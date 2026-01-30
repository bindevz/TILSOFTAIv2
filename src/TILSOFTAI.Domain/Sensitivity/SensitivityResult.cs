namespace TILSOFTAI.Domain.Sensitivity;

/// <summary>
/// Result of sensitivity classification.
/// </summary>
public sealed record SensitivityResult
{
    /// <summary>
    /// Indicates whether sensitive content was detected.
    /// </summary>
    public bool ContainsSensitive { get; init; }

    /// <summary>
    /// Human-readable reasons for the classification result.
    /// Used for observability, auditing, and tuning.
    /// </summary>
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
}
