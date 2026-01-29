using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Sql;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Modules.Model.Tools;

public sealed class ModelCountToolHandler : ModelToolHandlerBase, IToolHandler
{
    public ModelCountToolHandler(ISqlExecutor sqlExecutor) : base(sqlExecutor)
    {
    }

    public Task<string> ExecuteAsync(
        ToolDefinition tool,
        string argumentsJson,
        TilsoftExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (tool is null)
        {
            throw new ArgumentNullException(nameof(tool));
        }

        if (string.IsNullOrWhiteSpace(tool.SpName))
        {
            throw new InvalidOperationException("Tool SP name is required.");
        }

        using var args = ParseArguments(argumentsJson);
        string? season = null;
        if (args.RootElement.TryGetProperty("season", out var seasonNode)
            && seasonNode.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            season = seasonNode.GetString();
        }

        var parameters = new Dictionary<string, object?>
        {
            ["@Season"] = string.IsNullOrWhiteSpace(season) ? DBNull.Value : season
        };

        return ExecuteAsync(tool.SpName, context, parameters, cancellationToken);
    }
}
