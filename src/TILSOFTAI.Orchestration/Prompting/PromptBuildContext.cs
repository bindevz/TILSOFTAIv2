using TILSOFTAI.Orchestration.Policies;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Orchestration.Prompting;

/// <summary>
/// PATCH 36.02: Immutable per-request context carrier for prompt building.
/// Provides scoped tools, resolved modules, and runtime policy snapshot
/// without storing mutable state on singleton providers.
/// </summary>
public sealed class PromptBuildContext
{
    public IReadOnlyList<ToolDefinition> ScopedTools { get; }
    public IReadOnlyList<string> ResolvedModules { get; }
    public RuntimePolicySnapshot Policies { get; }

    public PromptBuildContext(
        IReadOnlyList<ToolDefinition> scopedTools,
        IReadOnlyList<string> resolvedModules,
        RuntimePolicySnapshot policies)
    {
        ScopedTools = scopedTools ?? Array.Empty<ToolDefinition>();
        ResolvedModules = resolvedModules ?? Array.Empty<string>();
        Policies = policies ?? RuntimePolicySnapshot.Empty;
    }

    /// <summary>
    /// Convenience: serialized module keys for SQL parameters.
    /// </summary>
    public string? ModuleKeysJson => ResolvedModules.Count > 0
        ? System.Text.Json.JsonSerializer.Serialize(ResolvedModules)
        : null;

    public static PromptBuildContext Empty { get; } =
        new(Array.Empty<ToolDefinition>(), Array.Empty<string>(), RuntimePolicySnapshot.Empty);
}
