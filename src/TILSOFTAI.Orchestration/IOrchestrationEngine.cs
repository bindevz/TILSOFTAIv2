using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Pipeline;

namespace TILSOFTAI.Orchestration;

public interface IOrchestrationEngine
{
    Task<ChatResult> RunChatAsync(ChatRequest request, TilsoftExecutionContext ctx, CancellationToken ct);
    
    IAsyncEnumerable<ChatStreamEvent> RunChatStreamAsync(
        ChatRequest request,
        TilsoftExecutionContext ctx,
        CancellationToken ct);
}
