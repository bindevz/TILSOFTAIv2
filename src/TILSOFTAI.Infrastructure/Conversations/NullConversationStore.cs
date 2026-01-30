using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Conversations;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Infrastructure.Conversations;

public sealed class NullConversationStore : IConversationStore
{
    public Task SaveUserMessageAsync(TilsoftExecutionContext context, ChatMessage message, RequestPolicy policy, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SaveAssistantMessageAsync(TilsoftExecutionContext context, ChatMessage message, RequestPolicy policy, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SaveToolExecutionAsync(TilsoftExecutionContext context, ToolExecutionRecord execution, RequestPolicy policy, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
