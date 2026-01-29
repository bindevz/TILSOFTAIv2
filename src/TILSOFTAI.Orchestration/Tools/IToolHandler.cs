using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Tools;

public interface IToolHandler
{
    Task<string> ExecuteAsync(ToolDefinition tool, string argumentsJson, TilsoftExecutionContext context, CancellationToken cancellationToken = default);
}
