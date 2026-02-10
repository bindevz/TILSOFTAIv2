namespace TILSOFTAI.Orchestration.Modules;

/// <summary>
/// Result of module scope resolution. Contains which modules 
/// the LLM selected for the current user query.
/// </summary>
public sealed record ModuleScopeResult
{
    /// <summary>Selected module keys (e.g., ["model"], ["model","analytics"])</summary>
    public IReadOnlyList<string> Modules { get; init; } = Array.Empty<string>();

    /// <summary>LLM confidence in module selection (0.0 - 1.0)</summary>
    public decimal Confidence { get; init; }

    /// <summary>LLM reasoning for module selection</summary>
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();

    /// <summary>Whether this result came from cache</summary>
    public bool FromCache { get; init; }
}
