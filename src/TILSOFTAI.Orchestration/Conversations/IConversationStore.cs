using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Orchestration.Conversations;

public interface IConversationStore
{
    Task SaveUserMessageAsync(TilsoftExecutionContext context, ChatMessage message, CancellationToken cancellationToken = default);
    Task SaveAssistantMessageAsync(TilsoftExecutionContext context, ChatMessage message, CancellationToken cancellationToken = default);
    Task SaveToolExecutionAsync(TilsoftExecutionContext context, ToolExecutionRecord execution, CancellationToken cancellationToken = default);
}
