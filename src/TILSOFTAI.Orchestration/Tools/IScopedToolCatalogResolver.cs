namespace TILSOFTAI.Orchestration.Tools;

/// <summary>
/// Extended tool catalog resolver that supports capability scoping.
/// Inherits from IToolCatalogResolver for backward compatibility.
/// </summary>
public interface IScopedToolCatalogResolver : IToolCatalogResolver
{
    /// <summary>
    /// Load tools scoped to the given capability scopes.
    /// Always includes native platform tools.
    /// </summary>
    Task<IReadOnlyList<ToolDefinition>> GetScopedToolsAsync(
        IReadOnlyList<string> capabilityScopes,
        CancellationToken cancellationToken = default);
}
