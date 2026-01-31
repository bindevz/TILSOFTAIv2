using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Contracts.Chat;
using TILSOFTAI.Api.Streaming;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Sensitivity;
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
    private readonly ISensitivityClassifier _sensitivityClassifier;
    private readonly LocalizationOptions _localizationOptions;

    public ChatHub(
        IOrchestrationEngine engine,
        IExecutionContextAccessor contextAccessor,
        ILogger<ChatHub> logger,
        ChatStreamEnvelopeFactory envelopeFactory,
        IOptions<StreamingOptions> streamingOptions,
        ISensitivityClassifier sensitivityClassifier,
        IOptions<LocalizationOptions> localizationOptions)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _envelopeFactory = envelopeFactory ?? throw new ArgumentNullException(nameof(envelopeFactory));
        _streamingOptions = streamingOptions ?? throw new ArgumentNullException(nameof(streamingOptions));
        _sensitivityClassifier = sensitivityClassifier ?? throw new ArgumentNullException(nameof(sensitivityClassifier));
        _localizationOptions = localizationOptions?.Value ?? new LocalizationOptions();
    }

    public async Task StartChat(ChatApiRequest request, CancellationToken cancellationToken = default)
    {
        var context = _contextAccessor.Current;

        // Fail-closed: Require valid execution context with tenant and user
        if (context is null ||
            string.IsNullOrWhiteSpace(context.TenantId) ||
            string.IsNullOrWhiteSpace(context.UserId))
        {
            throw new TilsoftApiException(
                ErrorCode.Unauthenticated,
                StatusCodes.Status401Unauthorized,
                detail: "Execution context not available or incomplete");
        }

        // Apply preferred language if provided and valid
        if (!string.IsNullOrWhiteSpace(request?.PreferredLanguage))
        {
            var validatedLanguage = ValidateAndNormalizeLanguage(request.PreferredLanguage);
            if (validatedLanguage is not null)
            {
                context.Language = validatedLanguage;
            }
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Context.ConnectionAborted);
        var linkedToken = linkedCts.Token;

        // Compute sensitivity server-side (ignore client value)
        var input = request?.Input ?? string.Empty;
        var sensitivityResult = _sensitivityClassifier.Classify(input);

        var chatRequest = new ChatRequest
        {
            Input = input,
            AllowCache = request?.AllowCache ?? true,
            ContainsSensitive = sensitivityResult.ContainsSensitive,
            SensitivityReasons = sensitivityResult.Reasons
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

#if DEBUG
    /// <summary>
    /// Test-only method to echo the current execution context.
    /// Enabled only in Development/Testing environments for contract testing.
    /// </summary>
    public Task<object> EchoContext()
    {
        var context = _contextAccessor.Current;

        // Fail-closed: Require valid execution context with tenant and user
        if (context is null ||
            string.IsNullOrWhiteSpace(context.TenantId) ||
            string.IsNullOrWhiteSpace(context.UserId))
        {
            throw new TilsoftApiException(
                ErrorCode.Unauthenticated,
                StatusCodes.Status401Unauthorized,
                detail: "Execution context not available or incomplete");
        }

        return Task.FromResult<object>(new
        {
            TenantId = context.TenantId,
            UserId = context.UserId,
            CorrelationId = context.CorrelationId,
            Language = context.Language
        });
    }
#endif

    private static bool IsTerminal(string? eventType)
    {
        return string.Equals(eventType, "final", StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, "error", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDelta(string? eventType)
    {
        return string.Equals(eventType, "delta", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates and normalizes a language code against supported languages.
    /// Returns normalized language if valid, null otherwise.
    /// </summary>
    private string? ValidateAndNormalizeLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var normalized = language.Trim().ToLowerInvariant();

        // Validate against supported languages
        if (_localizationOptions.SupportedLanguages != null &&
            _localizationOptions.SupportedLanguages.Length > 0)
        {
            var isSupported = _localizationOptions.SupportedLanguages
                .Any(lang => string.Equals(lang, normalized, StringComparison.OrdinalIgnoreCase));

            return isSupported ? normalized : null;
        }

        // If no supported languages configured, accept any language
        return normalized;
    }
}
