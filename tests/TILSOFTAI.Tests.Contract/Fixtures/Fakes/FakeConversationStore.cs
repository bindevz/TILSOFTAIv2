using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Conversations;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Tests.Contract.Fixtures.Fakes;

/// <summary>
/// Fake IConversationStore for contract tests that does not persist data.
/// </summary>
public sealed class FakeConversationStore : IConversationStore
{
    public Task SaveUserMessageAsync(TilsoftExecutionContext context, ChatMessage message, RequestPolicy policy, CancellationToken cancellationToken = default)
    {
        // No-op: do not persist in tests
        return Task.CompletedTask;
    }

    public Task SaveAssistantMessageAsync(TilsoftExecutionContext context, ChatMessage message, RequestPolicy policy, CancellationToken cancellationToken = default)
    {
        // No-op: do not persist in tests
        return Task.CompletedTask;
    }

    public Task SaveToolExecutionAsync(TilsoftExecutionContext context, ToolExecutionRecord execution, RequestPolicy policy, CancellationToken cancellationToken = default)
    {
        // No-op: do not persist in tests
        return Task.CompletedTask;
    }
}
