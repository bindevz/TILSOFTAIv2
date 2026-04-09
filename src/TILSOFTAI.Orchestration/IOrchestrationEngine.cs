using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Pipeline;

namespace TILSOFTAI.Orchestration;

[Obsolete("Use TILSOFTAI.Supervisor.ISupervisorRuntime. This interface remains as the Sprint 1 compatibility facade.")]
public interface IOrchestrationEngine
{
    Task<ChatResult> RunChatAsync(ChatRequest request, TilsoftExecutionContext ctx, CancellationToken ct);
    
    IAsyncEnumerable<ChatStreamEvent> RunChatStreamAsync(
        ChatRequest request,
        TilsoftExecutionContext ctx,
        CancellationToken ct);
}
