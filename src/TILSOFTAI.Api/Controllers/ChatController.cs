using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Contracts.Chat;
using TILSOFTAI.Api.Streaming;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration;
using TILSOFTAI.Orchestration.Pipeline;

namespace TILSOFTAI.Api.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public sealed class ChatController : ControllerBase
{
    private readonly IOrchestrationEngine _engine;
    private readonly IExecutionContextAccessor _contextAccessor;
    private readonly ILogger<ChatController> _logger;
    private readonly ChatStreamEnvelopeFactory _envelopeFactory;
    private readonly IOptions<StreamingOptions> _streamingOptions;

    public ChatController(
        IOrchestrationEngine engine,
        IExecutionContextAccessor contextAccessor,
        ILogger<ChatController> logger,
        ChatStreamEnvelopeFactory envelopeFactory,
        IOptions<StreamingOptions> streamingOptions)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _envelopeFactory = envelopeFactory ?? throw new ArgumentNullException(nameof(envelopeFactory));
        _streamingOptions = streamingOptions ?? throw new ArgumentNullException(nameof(streamingOptions));
    }

    [HttpPost]
    public async Task<ActionResult<ChatApiResponse>> Post([FromBody] ChatApiRequest request, CancellationToken cancellationToken)
    {
        var context = _contextAccessor.Current;
        var chatRequest = new ChatRequest
        {
            Input = request?.Input ?? string.Empty,
            AllowCache = request?.AllowCache ?? true,
            ContainsSensitive = request?.ContainsSensitive ?? false
        };

        var result = await _engine.RunChatAsync(chatRequest, context, cancellationToken);
        var response = new ChatApiResponse
        {
            Success = result.Success,
            Content = result.Success ? result.Content ?? string.Empty : string.Empty,
            ConversationId = context.ConversationId,
            CorrelationId = context.CorrelationId,
            TraceId = context.TraceId,
            Language = context.Language,
            Error = result.Success
                ? null
                : new ErrorEnvelope
                {
                    Code = ErrorCode.ChatFailed,
                    Message = result.Error ?? "Chat request failed."
                }
        };

        return result.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("stream")]
    public async Task Stream([FromBody] ChatApiRequest request, CancellationToken cancellationToken)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";

        var context = _contextAccessor.Current;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, HttpContext.RequestAborted);
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
                await SseWriter.WriteEventAsync(Response, envelope.Type, envelope, linkedToken);

                if (IsTerminal(evt.Type))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected or request aborted.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write SSE event.");
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
