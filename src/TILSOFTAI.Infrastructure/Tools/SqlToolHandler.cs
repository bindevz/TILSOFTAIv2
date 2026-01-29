using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Sql;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Infrastructure.Tools;

public sealed class SqlToolHandler : IToolHandler
{
    private readonly ISqlExecutor _sqlExecutor;

    public SqlToolHandler(ISqlExecutor sqlExecutor)
    {
        _sqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));
    }

    public Task<string> ExecuteAsync(ToolDefinition tool, string argumentsJson, TilsoftExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tool.SpName))
        {
            throw new InvalidOperationException($"Tool '{tool.Name}' does not specify an SP name.");
        }

        return _sqlExecutor.ExecuteToolAsync(tool.SpName, context.TenantId, argumentsJson, cancellationToken);
    }
}
