using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Contracts.OpenAi;
using TILSOFTAI.Api.Streaming;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Sensitivity;
using TILSOFTAI.Orchestration;
using TILSOFTAI.Orchestration.Conversations;
using TILSOFTAI.Orchestration.Pipeline;

namespace TILSOFTAI.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("v1/chat/completions")]
public sealed class OpenAiChatCompletionsController : ControllerBase
{
    private readonly IOrchestrationEngine _engine;
    private readonly IExecutionContextAccessor _contextAccessor;
    private readonly LlmOptions _llmOptions;
    private readonly ILogger<OpenAiChatCompletionsController> _logger;
    private readonly ChatStreamEnvelopeFactory _envelopeFactory;
    private readonly IOptions<StreamingOptions> _streamingOptions;
    private readonly IOptions<ChatOptions> _chatOptions;
    private readonly ISensitivityClassifier _sensitivityClassifier;

    public OpenAiChatCompletionsController(
        IOrchestrationEngine engine,
        IExecutionContextAccessor contextAccessor,
        IOptions<LlmOptions> llmOptions,
        ILogger<OpenAiChatCompletionsController> logger,
        ChatStreamEnvelopeFactory envelopeFactory,
        IOptions<StreamingOptions> streamingOptions,
        IOptions<ChatOptions> chatOptions,
        ISensitivityClassifier sensitivityClassifier)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        _llmOptions = llmOptions?.Value ?? throw new ArgumentNullException(nameof(llmOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _envelopeFactory = envelopeFactory ?? throw new ArgumentNullException(nameof(envelopeFactory));
        _streamingOptions = streamingOptions ?? throw new ArgumentNullException(nameof(streamingOptions));
        _chatOptions = chatOptions ?? throw new ArgumentNullException(nameof(chatOptions));
        _sensitivityClassifier = sensitivityClassifier ?? throw new ArgumentNullException(nameof(sensitivityClassifier));
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] OpenAiChatCompletionsRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentException("Request body is required.");
        }

        var joinedInput = BuildUserInput(request.Messages);
        
        // Enforce input size limit
        if (!string.IsNullOrEmpty(joinedInput) && joinedInput.Length > _chatOptions.Value.MaxInputChars)
        {
            throw new TilsoftApiException(
                ErrorCode.InvalidArgument,
                StatusCodes.Status400BadRequest,
                detail: new { maxInputChars = _chatOptions.Value.MaxInputChars });
        }

        //NKL SUPPORT FOR TEST;
        var context = _contextAccessor.Current ?? new TilsoftExecutionContext
        {
            TenantId = "public",
            UserId = "anonymous",
            Roles = new[] { "guest" },
            CorrelationId = Guid.NewGuid().ToString("N")
        };

        //var context = _contextAccessor.Current
        //    ?? throw new InvalidOperationException(
        //        "ExecutionContext is required. Ensure authentication middleware is configured.");
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var id = $"chatcmpl_{Guid.NewGuid():N}";
        var model = string.IsNullOrWhiteSpace(_llmOptions.Model) ? request.Model ?? "unknown" : _llmOptions.Model;

        if (request.Stream)
        {
            Response.StatusCode = StatusCodes.Status200OK;
            Response.ContentType = "text/event-stream";

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, HttpContext.RequestAborted);
            var linkedToken = linkedCts.Token;
            var translator = new OpenAiStreamTranslator(id, created, model);

            // Compute sensitivity server-side
            var sensitivityResult = _sensitivityClassifier.Classify(joinedInput);

            var chatRequest = new ChatRequest
            {
                Input = joinedInput,
                MessageHistory = BuildMessageHistory(request.Messages),
                AllowCache = true,
                ContainsSensitive = sensitivityResult.ContainsSensitive,
                SensitivityReasons = sensitivityResult.Reasons
            };

            try
            {
                await foreach (var evt in _engine.RunChatStreamAsync(chatRequest, context, linkedToken))
                {
                    var envelope = _envelopeFactory.Create(evt, context);
                    var hasChunk = translator.TryTranslate(envelope, out var chunk, out var isTerminal, out var isError);

                    if (hasChunk && chunk is not null)
                    {
                        await OpenAiSseWriter.WriteChunkAsync(Response, chunk, linkedToken);
                    }

                    if (isTerminal)
                    {
                        if (isError)
                        {
                            _logger.LogWarning("OpenAI stream error.");
                            await OpenAiSseWriter.WriteErrorAsync(Response, envelope.Payload ?? new { code = ErrorCode.ChatFailed }, linkedToken);
                        }

                        await OpenAiSseWriter.WriteDoneAsync(Response, linkedToken);
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
                _logger.LogError(ex, "Failed to write OpenAI SSE chunk.");
                if (!Response.HasStarted)
                {
                    throw;
                }
            }
            return new EmptyResult();
        }

        // Compute sensitivity server-side
        var sensitivityResultNonStream = _sensitivityClassifier.Classify(joinedInput);

        var chatRequestNonStream = new ChatRequest
        {
            Input = joinedInput,
            MessageHistory = BuildMessageHistory(request.Messages),
            AllowCache = true,
            ContainsSensitive = sensitivityResultNonStream.ContainsSensitive,
            SensitivityReasons = sensitivityResultNonStream.Reasons
        };

        var resultNonStream = await _engine.RunChatAsync(chatRequestNonStream, context, cancellationToken);
        if (!resultNonStream.Success)
        {
            var code = string.IsNullOrWhiteSpace(resultNonStream.Code) ? ErrorCode.ChatFailed : resultNonStream.Code;
            throw new TilsoftApiException(
                code,
                StatusCodes.Status400BadRequest,
                detail: resultNonStream.Detail ?? resultNonStream.Error);
        }

        var response = new OpenAiChatCompletionsResponse
        {
            Id = id,
            Created = created,
            Model = model,
            Choices =
            [
                new OpenAiChatChoice
                {
                    Index = 0,
                    Message = new OpenAiChatMessage
                    {
                        Role = "assistant",
                        Content = resultNonStream.Content ?? string.Empty
                    },
                    FinishReason = "stop"
                }
            ]
        };

        return Ok(response);
    }

    private static string BuildUserInput(IReadOnlyList<OpenAiChatMessage> messages)
    {
        if (messages is null || messages.Count == 0)
        {
            throw new ArgumentException("messages is required.");
        }

        var last = messages[^1];
        if (!string.Equals(last.Role, "user", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The last message must be a user message.");
        }

        return last.Content ?? string.Empty;
    }

    /// <summary>
    /// Builds conversation history from OpenAI-format messages.
    /// Includes user and assistant turns, limited to maxTurns most recent pairs.
    /// The last user message is excluded (it becomes Input).
    /// </summary>
    private static IReadOnlyList<ChatMessage> BuildMessageHistory(
        IReadOnlyList<OpenAiChatMessage> messages, int maxTurns = 10)
    {
        if (messages is null || messages.Count <= 1)
        {
            return Array.Empty<ChatMessage>();
        }

        var relevantMessages = messages
            .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Remove the last message (current input) — it's handled by BuildUserInput
        if (relevantMessages.Count > 0
            && string.Equals(relevantMessages[^1].Role, "user", StringComparison.OrdinalIgnoreCase))
        {
            relevantMessages.RemoveAt(relevantMessages.Count - 1);
        }

        // Take last N turns
        var history = new List<ChatMessage>();
        var startIndex = Math.Max(0, relevantMessages.Count - (maxTurns * 2));
        for (var i = startIndex; i < relevantMessages.Count; i++)
        {
            var msg = relevantMessages[i];
            history.Add(new ChatMessage(msg.Role!, msg.Content ?? string.Empty));
        }

        return history;
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
