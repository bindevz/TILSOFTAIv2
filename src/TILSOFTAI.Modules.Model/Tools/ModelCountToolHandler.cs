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

        // Pass raw argumentsJson to SP - it expects @TenantId and @ArgsJson
        // SP extracts optional "season" filter from JSON
        var json = string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson;
        return ExecuteToolAsync(tool.SpName, context, json, cancellationToken);
    }
}
