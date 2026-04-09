using TILSOFTAI.Agents.Abstractions;
using TILSOFTAI.Orchestration.Pipeline;
using TILSOFTAI.Supervisor;

namespace TILSOFTAI.Agents;

public sealed class LegacyChatDomainAgent : IDomainAgent
{
    public const string AgentIdValue = "legacy-chat";

    private readonly ChatPipeline _chatPipeline;

    public LegacyChatDomainAgent(ChatPipeline chatPipeline)
    {
        _chatPipeline = chatPipeline ?? throw new ArgumentNullException(nameof(chatPipeline));
    }

    public string AgentId => AgentIdValue;

    public string DisplayName => "Legacy Chat Domain Agent";

    public IReadOnlyList<string> OwnedDomains { get; } = new[] { "legacy-chat", "cross-domain" };

    public bool CanHandle(AgentTask task)
    {
        if (task is null)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(task.DomainHint)
            || string.Equals(task.DomainHint, "legacy-chat", StringComparison.OrdinalIgnoreCase)
            || string.Equals(task.IntentType, "chat", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<AgentResult> ExecuteAsync(AgentTask task, AgentExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(context);

        var chatRequest = new ChatRequest
        {
            Input = task.Input,
            Stream = task.Stream,
            StreamObserver = task.StreamObserver is null
                ? null
                : new InlineProgress<ChatStreamEvent>(evt => task.StreamObserver.Report(SupervisorStreamEvent.FromChat(evt))),
            AllowCache = task.AllowCache,
            ContainsSensitive = task.ContainsSensitive,
            SensitivityReasons = task.SensitivityReasons,
            RequestPolicy = task.RequestPolicy,
            MessageHistory = task.MessageHistory
        };

        var result = await _chatPipeline.RunAsync(chatRequest, context.RuntimeContext, ct);
        return result.Success
            ? AgentResult.Ok(result.Content ?? string.Empty)
            : AgentResult.Fail(result.Error ?? "Chat request failed.", result.Code, result.Detail);
    }

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
