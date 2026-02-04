using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Contracts.Chat;
using TILSOFTAI.Api.Streaming;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Sensitivity;
using TILSOFTAI.Orchestration;
using TILSOFTAI.Orchestration.Pipeline;

namespace TILSOFTAI.Api.Controllers;

[ApiController]
[Route("api/chats")]
//[Authorize]
[AllowAnonymous]
public sealed class ChatController : ControllerBase
{
    private readonly IOrchestrationEngine _engine;
    private readonly IExecutionContextAccessor _contextAccessor;
    private readonly ILogger<ChatController> _logger;
    private readonly ChatStreamEnvelopeFactory _envelopeFactory;
    private readonly IOptions<StreamingOptions> _streamingOptions;
    private readonly IOptions<ChatOptions> _chatOptions;
    private readonly ISensitivityClassifier _sensitivityClassifier;

    public ChatController(
        IOrchestrationEngine engine,
        IExecutionContextAccessor contextAccessor,
        ILogger<ChatController> logger,
        ChatStreamEnvelopeFactory envelopeFactory,
        IOptions<StreamingOptions> streamingOptions,
        IOptions<ChatOptions> chatOptions,
        ISensitivityClassifier sensitivityClassifier)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _envelopeFactory = envelopeFactory ?? throw new ArgumentNullException(nameof(envelopeFactory));
        _streamingOptions = streamingOptions ?? throw new ArgumentNullException(nameof(streamingOptions));
        _chatOptions = chatOptions ?? throw new ArgumentNullException(nameof(chatOptions));
        _sensitivityClassifier = sensitivityClassifier ?? throw new ArgumentNullException(nameof(sensitivityClassifier));
    }

    [HttpPost]
    public async Task<ActionResult<ChatApiResponse>> Post([FromBody] ChatApiRequest request, CancellationToken cancellationToken)
    {
        var context = _contextAccessor.Current;
        
        // Compute sensitivity server-side (ignore client value)
        var input = request?.Input ?? string.Empty;
        
        // Enforce input size limit
        if (!string.IsNullOrEmpty(input) && input.Length > _chatOptions.Value.MaxInputChars)
        {
            throw new TilsoftApiException(
                ErrorCode.InvalidArgument,
                StatusCodes.Status400BadRequest,
                detail: new { maxInputChars = _chatOptions.Value.MaxInputChars });
        }
        var sensitivityResult = _sensitivityClassifier.Classify(input);
        
        var chatRequest = new ChatRequest
        {
            Input = input,
            AllowCache = request?.AllowCache ?? true,
            ContainsSensitive = sensitivityResult.ContainsSensitive,
            SensitivityReasons = sensitivityResult.Reasons
        };

        var result = await _engine.RunChatAsync(chatRequest, context, cancellationToken);
        
        if (!result.Success)
        {
            var code = string.IsNullOrWhiteSpace(result.Code) ? ErrorCode.ChatFailed : result.Code;
            throw new TilsoftApiException(
                code,
                StatusCodes.Status400BadRequest,
                detail: result.Detail ?? result.Error);
        }
        
        var response = new ChatApiResponse
        {
            Success = true,
            Content = result.Content ?? string.Empty,
            ConversationId = context.ConversationId,
            CorrelationId = context.CorrelationId,
            TraceId = context.TraceId,
            Language = context.Language
        };

        return Ok(response);
    }

    [HttpPost("stream")]
    public async Task Stream([FromBody] ChatApiRequest request, CancellationToken cancellationToken)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";

        var context = _contextAccessor.Current;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, HttpContext.RequestAborted);
        var linkedToken = linkedCts.Token;

        // Compute sensitivity server-side (ignore client value)
        var input = request?.Input ?? string.Empty;
        
        // Enforce input size limit
        if (!string.IsNullOrEmpty(input) && input.Length > _chatOptions.Value.MaxInputChars)
        {
            throw new TilsoftApiException(
                ErrorCode.InvalidArgument,
                StatusCodes.Status400BadRequest,
                detail: new { maxInputChars = _chatOptions.Value.MaxInputChars });
        }
        var sensitivityResult = _sensitivityClassifier.Classify(input);

        var chatRequest = new ChatRequest
        {
            Input = input,
            AllowCache = request?.AllowCache ?? true,
            ContainsSensitive = sensitivityResult.ContainsSensitive,
            SensitivityReasons = sensitivityResult.Reasons
        };

#if DEBUG
        // Test-only hook: Trigger deterministic error for contract testing
        // Only enabled in Testing environment
        if (HttpContext.Request.Headers.TryGetValue("X-Test-Trigger-Error", out var triggerErrorValue) 
            && triggerErrorValue == "true")
        {
            throw new TilsoftApiException(
                ErrorCode.ChatFailed,
                StatusCodes.Status400BadRequest,
                detail: new { testError = "Intentional error for SSE contract testing" });
        }
#endif

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
