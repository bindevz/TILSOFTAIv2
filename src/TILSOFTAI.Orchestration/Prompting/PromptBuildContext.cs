using TILSOFTAI.Orchestration.Policies;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Orchestration.Prompting;

/// <summary>
/// PATCH 36.02: Immutable per-request context carrier for prompt building.
/// Provides scoped tools, resolved capability scopes, and runtime policy snapshot
/// without storing mutable state on singleton providers.
/// </summary>
public sealed class PromptBuildContext
{
    public IReadOnlyList<ToolDefinition> ScopedTools { get; }
    public IReadOnlyList<string> ResolvedCapabilityScopes { get; }
    public RuntimePolicySnapshot Policies { get; }

    public PromptBuildContext(
        IReadOnlyList<ToolDefinition> scopedTools,
        IReadOnlyList<string> resolvedCapabilityScopes,
        RuntimePolicySnapshot policies)
    {
        ScopedTools = scopedTools ?? Array.Empty<ToolDefinition>();
        ResolvedCapabilityScopes = resolvedCapabilityScopes ?? Array.Empty<string>();
        Policies = policies ?? RuntimePolicySnapshot.Empty;
    }

    /// <summary>
    /// Convenience: serialized capability scopes for SQL compatibility parameters.
    /// </summary>
    public string? CapabilityScopesJson => ResolvedCapabilityScopes.Count > 0
        ? System.Text.Json.JsonSerializer.Serialize(ResolvedCapabilityScopes)
        : null;

    public static PromptBuildContext Empty { get; } =
        new(Array.Empty<ToolDefinition>(), Array.Empty<string>(), RuntimePolicySnapshot.Empty);
}
