using TILSOFTAI.Agents.Abstractions;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Pipeline;
using TILSOFTAI.Supervisor;

namespace TILSOFTAI.Agents;

/// <summary>
/// Sprint 2 bridge: delegates agent execution to the legacy ChatPipeline.
/// All domain agents that do not yet own their own execution loop use this bridge.
/// This is a temporary compatibility shim — domain-native execution replaces it in future sprints.
///
/// TRANSITIONAL COMPONENT — Sprint 5 compatibility debt:
///   Why it still exists: Fallback path for AccountingAgent (non-matched capabilities) and LegacyChatDomainAgent (catch-all).
///   What depends on it: DomainAgentBase default ExecuteAsync, LegacyChatDomainAgent, agents when no native capability matches.
///   What removes it: Sprint 7+ — when all domain agents have full native capability coverage for their domains.
/// </summary>
public sealed class LegacyChatPipelineBridge
{
    private readonly ChatPipeline _chatPipeline;

    public LegacyChatPipelineBridge(ChatPipeline chatPipeline)
    {
        _chatPipeline = chatPipeline ?? throw new ArgumentNullException(nameof(chatPipeline));
    }

    /// <summary>
    /// Execute a domain agent task by delegating to the legacy ChatPipeline.
    /// </summary>
    /// <param name="task">The agent task to execute.</param>
    /// <param name="context">Agent execution context with runtime context and approval engine.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>AgentResult mapped from ChatResult.</returns>
    public async Task<AgentResult> ExecuteAsync(AgentTask task, AgentExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(context);

        var chatRequest = BuildChatRequest(task);
        var result = await _chatPipeline.RunAsync(chatRequest, context.RuntimeContext, ct);

        return result.Success
            ? AgentResult.Ok(result.Content ?? string.Empty)
            : AgentResult.Fail(result.Error ?? "Chat request failed.", result.Code, result.Detail);
    }

    private static ChatRequest BuildChatRequest(AgentTask task) => new()
    {
        Input = task.Input,
        Stream = task.Stream,
        StreamObserver = task.StreamObserver is null
            ? null
            : new InlineProgress<ChatStreamEvent>(
                evt => task.StreamObserver.Report(SupervisorStreamEvent.FromChat(evt))),
        AllowCache = task.AllowCache,
        ContainsSensitive = task.ContainsSensitive,
        SensitivityReasons = task.SensitivityReasons,
        RequestPolicy = task.RequestPolicy,
        MessageHistory = task.MessageHistory
    };

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public InlineProgress(Action<T> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void Report(T value) => _handler(value);
    }
}
