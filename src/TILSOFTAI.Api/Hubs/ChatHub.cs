using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Contracts.Chat;
using TILSOFTAI.Api.Streaming;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration;
using TILSOFTAI.Orchestration.Pipeline;

namespace TILSOFTAI.Api.Hubs;

[Authorize]
public sealed class ChatHub : Hub
{
    private readonly IOrchestrationEngine _engine;
    private readonly IExecutionContextAccessor _contextAccessor;
    private readonly ILogger<ChatHub> _logger;
    private readonly ChatStreamEnvelopeFactory _envelopeFactory;
    private readonly IOptions<StreamingOptions> _streamingOptions;

    public ChatHub(
        IOrchestrationEngine engine,
        IExecutionContextAccessor contextAccessor,
        ILogger<ChatHub> logger,
        ChatStreamEnvelopeFactory envelopeFactory,
        IOptions<StreamingOptions> streamingOptions)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _envelopeFactory = envelopeFactory ?? throw new ArgumentNullException(nameof(envelopeFactory));
        _streamingOptions = streamingOptions ?? throw new ArgumentNullException(nameof(streamingOptions));
    }

    public async Task StartChat(ChatApiRequest request, CancellationToken cancellationToken = default)
    {
        var context = _contextAccessor.Current;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Context.ConnectionAborted);
        var linkedToken = linkedCts.Token;

        var chatRequest = new ChatRequest
        {
            Input = request?.Input ?? string.Empty,
            AllowCache = request?.AllowCache ?? true,
            ContainsSensitive = request?.ContainsSensitive ?? false
        };

        try
        {
            await foreach (var evt in _engine.RunChatStreamAsync(chatRequest, context, linkedToken))
            {
                var envelope = _envelopeFactory.Create(evt, context);

                try
                {
                    await Clients.Caller.SendAsync("chat_event", envelope, linkedToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to emit chat event. ConversationId: {ConversationId} CorrelationId: {CorrelationId}",
                        envelope.ConversationId,
                        envelope.CorrelationId);
                    break;
                }

                if (IsTerminal(evt.Type))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Connection aborted.
        }
    }

    private static bool IsTerminal(string? eventType)
    {
        return string.Equals(eventType, "final", StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, "error", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDelta(string? eventType)
    {
        return string.Equals(eventType, "delta", StringComparison.OrdinalIgnoreCase);
    }
}
