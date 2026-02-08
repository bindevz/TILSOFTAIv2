using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Sql;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Modules.Model.Tools;

public sealed class ModelGetPiecesToolHandler : ModelToolHandlerBase, IToolHandler
{
    public ModelGetPiecesToolHandler(ISqlExecutor sqlExecutor) : base(sqlExecutor)
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

        // Validate arguments (modelId required)
        using var args = ParseArguments(argumentsJson);
        _ = RequireInt(args.RootElement, "modelId");

        // Pass raw argumentsJson to SP - it expects @TenantId and @ArgsJson
        return ExecuteToolAsync(tool.SpName, context, argumentsJson, cancellationToken);
    }
}
