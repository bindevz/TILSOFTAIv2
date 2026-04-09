using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Tools;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Infrastructure.Tools;

public sealed class SqlToolHandler : IToolHandler
{
    private readonly IToolAdapterRegistry _toolAdapterRegistry;

    public SqlToolHandler(IToolAdapterRegistry toolAdapterRegistry)
    {
        _toolAdapterRegistry = toolAdapterRegistry ?? throw new ArgumentNullException(nameof(toolAdapterRegistry));
    }

    public async Task<string> ExecuteAsync(ToolDefinition tool, string argumentsJson, TilsoftExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tool.SpName))
        {
            throw new InvalidOperationException($"Tool '{tool.Name}' does not specify an SP name.");
        }

        var adapter = _toolAdapterRegistry.Resolve("sql");
        var result = await adapter.ExecuteAsync(
            new ToolExecutionRequest
            {
                TenantId = context.TenantId,
                AgentId = "legacy-chat",
                SystemId = "sql",
                CapabilityKey = tool.Name,
                Operation = ToolAdapterOperationNames.ExecuteTool,
                ArgumentsJson = argumentsJson,
                CorrelationId = context.CorrelationId,
                Metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["storedProcedure"] = tool.SpName
                }
            },
            cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"SQL adapter execution failed: {result.ErrorCode ?? "UNKNOWN_ERROR"}");
        }

        return result.PayloadJson ?? string.Empty;
    }
}
