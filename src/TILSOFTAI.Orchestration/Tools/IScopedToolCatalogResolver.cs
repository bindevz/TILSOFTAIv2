namespace TILSOFTAI.Orchestration.Tools;

/// <summary>
/// Extended tool catalog resolver that supports module scoping.
/// Inherits from IToolCatalogResolver for backward compatibility.
/// </summary>
public interface IScopedToolCatalogResolver : IToolCatalogResolver
{
    /// <summary>
    /// Load tools scoped to the given modules.
    /// Always includes platform tools.
    /// </summary>
    Task<IReadOnlyList<ToolDefinition>> GetScopedToolsAsync(
        IReadOnlyList<string> moduleKeys,
        CancellationToken cancellationToken = default);
}
