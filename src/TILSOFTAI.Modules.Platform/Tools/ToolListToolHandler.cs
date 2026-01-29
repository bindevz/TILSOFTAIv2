using System.Text.Json;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Modules.Platform.Tools;

public sealed class ToolListToolHandler : IToolHandler
{
    private readonly IToolCatalogResolver _toolCatalogResolver;

    public ToolListToolHandler(IToolCatalogResolver toolCatalogResolver)
    {
        _toolCatalogResolver = toolCatalogResolver ?? throw new ArgumentNullException(nameof(toolCatalogResolver));
    }

    public async Task<string> ExecuteAsync(
        ToolDefinition tool,
        string argumentsJson,
        TilsoftExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (tool is null)
        {
            throw new ArgumentNullException(nameof(tool));
        }

        var tools = await _toolCatalogResolver.GetResolvedToolsAsync(cancellationToken);

        var result = new
        {
            tools = tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                jsonSchema = t.JsonSchema,
                module = t.Module
            }).ToArray()
        };

        return JsonSerializer.Serialize(result);
    }
}
