using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Contracts.OpenAi;
using TILSOFTAI.Api.Streaming;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration;
using TILSOFTAI.Orchestration.Pipeline;

namespace TILSOFTAI.Api.Controllers;

[ApiController]
[Route("v1/chat/completions")]
[Authorize]
public sealed class OpenAiChatCompletionsController : ControllerBase
{
    private readonly IOrchestrationEngine _engine;
    private readonly IExecutionContextAccessor _contextAccessor;
    private readonly LlmOptions _llmOptions;
    private readonly ILogger<OpenAiChatCompletionsController> _logger;
    private readonly ChatStreamEnvelopeFactory _envelopeFactory;
    private readonly IOptions<StreamingOptions> _streamingOptions;

    public OpenAiChatCompletionsController(
        IOrchestrationEngine engine,
        IExecutionContextAccessor contextAccessor,
        IOptions<LlmOptions> llmOptions,
        ILogger<OpenAiChatCompletionsController> logger,
        ChatStreamEnvelopeFactory envelopeFactory,
        IOptions<StreamingOptions> streamingOptions)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        _llmOptions = llmOptions?.Value ?? throw new ArgumentNullException(nameof(llmOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _envelopeFactory = envelopeFactory ?? throw new ArgumentNullException(nameof(envelopeFactory));
        _streamingOptions = streamingOptions ?? throw new ArgumentNullException(nameof(streamingOptions));
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] OpenAiChatCompletionsRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentException("Request body is required.");
        }

        var joinedInput = BuildUserInput(request.Messages);
        var context = _contextAccessor.Current;
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

            var chatRequest = new ChatRequest
            {
                Input = joinedInput,
                AllowCache = true,
                ContainsSensitive = false
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
                            _logger.LogWarning("OpenAI stream error: {Error}", envelope.Payload);
                            if (!Response.HasStarted)
                            {
                                throw new InvalidOperationException(envelope.Payload?.ToString() ?? "OpenAI stream error.");
                            }
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

        var chatRequestNonStream = new ChatRequest
        {
            Input = joinedInput,
            AllowCache = true,
            ContainsSensitive = false
        };

        var resultNonStream = await _engine.RunChatAsync(chatRequestNonStream, context, cancellationToken);
        if (!resultNonStream.Success)
        {
            throw new InvalidOperationException(resultNonStream.Error ?? "Chat request failed.");
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

        var userMessages = messages
            .Where(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
            .Select(message => message.Content ?? string.Empty)
            .ToList();

        if (userMessages.Count == 0)
        {
            throw new ArgumentException("At least one user message is required.");
        }

        return string.Join("\n", userMessages);
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
