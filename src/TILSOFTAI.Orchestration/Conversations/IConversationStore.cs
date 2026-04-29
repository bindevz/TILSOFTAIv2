using TILSOFTAI.Domain.ExecutionContext;
namespace TILSOFTAI.Orchestration.Conversations;

public interface IConversationStore
{
    Task SaveUserMessageAsync(TilsoftExecutionContext context, ChatMessage message, RequestPolicy policy, CancellationToken cancellationToken = default);
    Task SaveAssistantMessageAsync(TilsoftExecutionContext context, ChatMessage message, RequestPolicy policy, CancellationToken cancellationToken = default);
}
