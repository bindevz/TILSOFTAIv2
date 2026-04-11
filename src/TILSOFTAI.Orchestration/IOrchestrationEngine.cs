using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Pipeline;

namespace TILSOFTAI.Orchestration;

/// <summary>
/// Sprint 1 compatibility facade for the orchestration engine.
///
/// TRANSITIONAL COMPONENT — Sprint 5 compatibility debt:
///   Why it still exists: Controllers and hubs still reference IOrchestrationEngine instead of ISupervisorRuntime.
///   What depends on it: ChatController, ChatHub, and any external integration referencing this interface.
///   What removes it: Sprint 6+ — migrate all controllers/hubs to ISupervisorRuntime, then delete this interface.
/// </summary>
[Obsolete("Sprint 5: Use ISupervisorRuntime directly. This facade will be removed when all controllers are migrated.")]
public interface IOrchestrationEngine
{
    Task<ChatResult> RunChatAsync(ChatRequest request, TilsoftExecutionContext ctx, CancellationToken ct);
    
    IAsyncEnumerable<ChatStreamEvent> RunChatStreamAsync(
        ChatRequest request,
        TilsoftExecutionContext ctx,
        CancellationToken ct);
}
