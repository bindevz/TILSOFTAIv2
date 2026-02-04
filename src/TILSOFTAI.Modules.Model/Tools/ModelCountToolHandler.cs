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

        // Pass the full argumentsJson as @ArgsJson per Patch 26.01 standard contract
        // The stored procedure will extract the optional "season" filter from the JSON
        var parameters = new Dictionary<string, object?>
        {
            ["@ArgsJson"] = string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson
        };

        return ExecuteAsync(tool.SpName, context, parameters, cancellationToken);
    }
}
