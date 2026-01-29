namespace TILSOFTAI.Orchestration.Tools;

public interface IToolCatalogResolver
{
    Task<IReadOnlyList<ToolDefinition>> GetResolvedToolsAsync(CancellationToken cancellationToken = default);
}
