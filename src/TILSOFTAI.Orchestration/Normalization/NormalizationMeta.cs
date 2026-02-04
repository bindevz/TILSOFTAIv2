namespace TILSOFTAI.Orchestration.Normalization;

/// <summary>
/// Metadata captured during input normalization.
/// </summary>
public sealed class NormalizationMeta
{
    /// <summary>
    /// Original raw input before normalization.
    /// </summary>
    public string RawInput { get; set; } = string.Empty;

    /// <summary>
    /// Normalized output.
    /// </summary>
    public string NormalizedInput { get; set; } = string.Empty;

    /// <summary>
    /// Detected language hint.
    /// </summary>
    public string LanguageHint { get; set; } = "en";

    /// <summary>
    /// Applied transformations.
    /// </summary>
    public List<string> Transformations { get; set; } = new();

    /// <summary>
    /// Season normalizations applied (e.g., "25/26 -> 2025/2026").
    /// </summary>
    public Dictionary<string, string> SeasonNormalizations { get; set; } = new();

    /// <summary>
    /// Whether whitespace was normalized.
    /// </summary>
    public bool WhitespaceNormalized { get; set; }
}
