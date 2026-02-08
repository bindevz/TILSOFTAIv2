using System.Net.Http;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Analytics;
using TILSOFTAI.Orchestration.Caching;
using TILSOFTAI.Orchestration.Compaction;
using TILSOFTAI.Orchestration.Conversations;
using TILSOFTAI.Orchestration.Llm;
using TILSOFTAI.Orchestration.Normalization;
using TILSOFTAI.Orchestration.Policies;
using TILSOFTAI.Orchestration.Prompting;
using TILSOFTAI.Orchestration.Tools;
using TILSOFTAI.Domain.Validation;
using TILSOFTAI.Domain.Metrics;

namespace TILSOFTAI.Orchestration.Pipeline;

public sealed class ChatPipeline
{
    private readonly INormalizationService _normalizationService;
    private readonly IConversationStore _conversationStore;
    private readonly PromptBuilder _promptBuilder;
    private readonly ILlmClient _llmClient;
    private readonly IToolCatalogResolver _toolCatalogResolver;
    private readonly ToolGovernance _toolGovernance;
    private readonly IToolHandler _toolHandler;
    private readonly ToolResultCompactor _toolResultCompactor;
    private readonly ISemanticCache _semanticCache;
    private readonly RecursionPolicy _recursionPolicy;
    private readonly IInputValidator _inputValidator;
    private readonly ChatOptions _chatOptions;
    private readonly AnalyticsOptions _analyticsOptions;
    private readonly AnalyticsIntentDetector _intentDetector;
    private readonly AnalyticsOrchestrator? _analyticsOrchestrator;
    private readonly ILogger<ChatPipeline> _logger;

    public ChatPipeline(
        INormalizationService normalizationService,
        IConversationStore conversationStore,
        PromptBuilder promptBuilder,
        ILlmClient llmClient,
        IToolCatalogResolver toolCatalogResolver,
        ToolGovernance toolGovernance,
        IToolHandler toolHandler,
        ToolResultCompactor toolResultCompactor,
        ISemanticCache semanticCache,
        RecursionPolicy recursionPolicy,
        IInputValidator inputValidator,
        IOptions<ChatOptions> chatOptions,
        IOptions<AnalyticsOptions> analyticsOptions,
        AnalyticsIntentDetector intentDetector,
        AnalyticsOrchestrator? analyticsOrchestrator,
        ILogger<ChatPipeline> logger,
        Observability.ChatPipelineInstrumentation instrumentation,
        IMetricsService metrics)
    {
        _normalizationService = normalizationService ?? throw new ArgumentNullException(nameof(normalizationService));
        _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
        _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _toolCatalogResolver = toolCatalogResolver ?? throw new ArgumentNullException(nameof(toolCatalogResolver));
        _toolGovernance = toolGovernance ?? throw new ArgumentNullException(nameof(toolGovernance));
        _toolHandler = toolHandler ?? throw new ArgumentNullException(nameof(toolHandler));
        _toolResultCompactor = toolResultCompactor ?? throw new ArgumentNullException(nameof(toolResultCompactor));
        _semanticCache = semanticCache ?? throw new ArgumentNullException(nameof(semanticCache));
        _recursionPolicy = recursionPolicy ?? throw new ArgumentNullException(nameof(recursionPolicy));
        _inputValidator = inputValidator ?? throw new ArgumentNullException(nameof(inputValidator));
        _chatOptions = chatOptions?.Value ?? throw new ArgumentNullException(nameof(chatOptions));
        _analyticsOptions = analyticsOptions?.Value ?? new AnalyticsOptions();
        _intentDetector = intentDetector ?? throw new ArgumentNullException(nameof(intentDetector));
        _analyticsOrchestrator = analyticsOrchestrator; // Optional, null if not registered
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _instrumentation = instrumentation ?? throw new ArgumentNullException(nameof(instrumentation));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    private readonly Observability.ChatPipelineInstrumentation _instrumentation;
    private readonly IMetricsService _metrics;

    public async Task<ChatResult> RunAsync(ChatRequest request, TilsoftExecutionContext ctx, CancellationToken ct)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var pipelineStopwatch = Stopwatch.StartNew();
        using var activity = _instrumentation.StartPipeline(ctx.ConversationId, ctx.TenantId);
        using var timer = _metrics.CreateTimer(MetricNames.ChatPipelineDurationSeconds); // Labels added if needed (e.g. tenant)

        _logger.LogInformation(
            "PipelineStarted | ConversationId: {ConversationId} | TenantId: {TenantId} | UserId: {UserId}",
            ctx.ConversationId, ctx.TenantId, ctx.UserId);

        _recursionPolicy.Reset();

        // Validate user input at pipeline entry
        var validationResult = _inputValidator.ValidateUserInput(request.Input, InputContext.ForChatMessage());
        if (!validationResult.IsValid)
        {
            var firstError = validationResult.Errors.FirstOrDefault();
            _logger.LogWarning(
                "Pipeline input validation failed. Code: {Code}, Field: {Field}",
                firstError?.Code, firstError?.Field);
            return Fail(request, firstError?.Message ?? "Input validation failed.", firstError?.Code);
        }

        // Log validation metrics
        if (validationResult.InjectionSeverity != PromptInjectionSeverity.None)
        {
            _logger.LogWarning(
                "Prompt injection detected in pipeline. Severity: {Severity}",
                validationResult.InjectionSeverity);
        }

        // Use sanitized input
        var sanitizedInput = validationResult.SanitizedValue ?? request.Input;
        var normalized = await _normalizationService.NormalizeAsync(sanitizedInput, ctx, ct);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Fail(request, "Input is empty.");
        }

        var userMessage = new ChatMessage(ChatRoles.User, normalized);
        var policy = request.RequestPolicy ?? new RequestPolicy
        {
            ContainsSensitive = request.ContainsSensitive
        };
        await _conversationStore.SaveUserMessageAsync(ctx, userMessage, policy, ct);

        var messages = new List<LlmMessage>
        {
            new(ChatRoles.User, normalized)
        };

        var tools = await _toolCatalogResolver.GetResolvedToolsAsync(ct);
        var toolLookup = tools.ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);

        var containsSensitive = policy.ContainsSensitive;

        if (request.AllowCache && _semanticCache.Enabled && !policy.ShouldBypassCache)
        {
            var cacheResult = await _semanticCache.TryGetAnswerAsync(
                ctx,
                "chat",
                normalized,
                tools,
                null,
                containsSensitive,
                ct);

            var cached = cacheResult.Match(
                onSuccess: answer => answer,
                onFailure: error =>
                {
                    _logger.LogWarning("Cache retrieval failed: {Code} - {Message}", error.Code, error.Message);
                    return null; // Graceful degradation - proceed without cache
                });

            if (!string.IsNullOrWhiteSpace(cached))
            {
                request.StreamObserver?.Report(ChatStreamEvent.Final(cached));
                return ChatResult.Ok(cached);
            }
        }

        // PATCH 29.02: Detect analytics intent and route to deterministic orchestrator
        // PATCH 30.04: LLM-first detection with catalog_search tie-breaker for borderline
        if (_analyticsOptions.Enabled && _analyticsOrchestrator != null)
        {
            var intent = await _intentDetector.DetectAsync(normalized, ct);
            
            // ========== PATCH 31.07: RBAC Gate ==========
            // Fast role check BEFORE routing to analytics orchestrator
            if (intent.IsAnalytics)
            {
                var requiredRole = _analyticsOptions.RequiredRole ?? "analytics.read";
                var userRoles = new HashSet<string>(
                    ctx.Roles ?? Array.Empty<string>(), 
                    StringComparer.OrdinalIgnoreCase);
                
                if (!userRoles.Contains(requiredRole))
                {
                    _logger.LogWarning(
                        "AnalyticsRbacDenied | UserId: {UserId} | TenantId: {TenantId} | " +
                        "RequiredRole: {RequiredRole} | UserRoles: [{UserRoles}]",
                        ctx.UserId, ctx.TenantId, requiredRole,
                        string.Join(", ", ctx.Roles ?? Array.Empty<string>()));
                    
                    // Neutralize intent → fall through to normal LLM flow
                    intent = AnalyticsIntentResult.None();
                }
            }
            // ========== END PATCH 31.07 ==========
            
            // T02: Borderline tie-breaker via catalog_search (max 1 extra call)
            if (intent.IsBorderline && !string.IsNullOrEmpty(intent.EntityHint))
            {
                _logger.LogInformation(
                    "AnalyticsIntentBorderline | Confidence: {Confidence} | EntityHint: {EntityHint} | Trying catalog_search",
                    intent.Confidence, intent.EntityHint);
                
                // Quick catalog_search to resolve borderline
                var catalogResult = await _analyticsOrchestrator.TryCatalogSearchAsync(intent.EntityHint, ctx, ct);
                if (catalogResult.HasResults)
                {
                    intent = intent with { IsAnalytics = true };
                    intent.Hints.Add("catalog_tiebreaker");
                }
            }
            
            if (intent.IsAnalytics)
            {
                _logger.LogInformation(
                    "AnalyticsIntentDetected | Confidence: {Confidence} | Hints: {Hints}",
                    intent.Confidence, string.Join(", ", intent.Hints));

                var analyticsResult = await _analyticsOrchestrator.ExecuteAsync(normalized, intent, ctx, ct);
                
                if (analyticsResult.Success && !string.IsNullOrEmpty(analyticsResult.Content))
                {
                    var assistantMessage = new ChatMessage(ChatRoles.Assistant, analyticsResult.Content);
                    await _conversationStore.SaveAssistantMessageAsync(ctx, assistantMessage, policy, ct);
                    request.StreamObserver?.Report(ChatStreamEvent.Final(analyticsResult.Content));

                    pipelineStopwatch.Stop();
                    _logger.LogInformation(
                        "PipelineCompleted | ConversationId: {ConversationId} | Duration: {DurationMs}ms | Mode: Analytics | ToolCalls: {ToolCalls}",
                        ctx.ConversationId, pipelineStopwatch.ElapsedMilliseconds, analyticsResult.ToolCallCount);

                    return ChatResult.Ok(analyticsResult.Content);
                }
                else if (!analyticsResult.Success)
                {
                    _logger.LogWarning(
                        "AnalyticsOrchestratorFailed | Error: {Error} | Falling back to normal flow",
                        analyticsResult.ErrorMessage);
                    // Fall through to normal tool-calling flow
                }
            }
        }

        for (var step = 0; step < _chatOptions.MaxSteps; step++)
        {
            var llmRequest = await _promptBuilder.BuildAsync(messages, tools, ctx, ct);
            llmRequest.Stream = request.Stream;

            var llmStopwatch = Stopwatch.StartNew();
            _logger.LogDebug(
                "LlmRequestSent | Step: {Step} | MessageCount: {MessageCount} | ToolCount: {ToolCount}",
                step, messages.Count, tools.Count);

            LlmResponse response;
            try
            {
                response = request.Stream
                    ? await CompleteViaStreamAsync(llmRequest, request, ct)
                    : await _llmClient.CompleteAsync(llmRequest, ct);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("504") || ex.Message.Contains("Gateway"))
            {
                _logger.LogError(ex, "LLM server timeout (504 Gateway). The AI service is temporarily unavailable.");
                var errorMessage = "Xin lỗi, máy chủ AI đang bận. Vui lòng thử lại sau vài giây. (AI server is busy. Please try again in a few seconds.)";
                request.StreamObserver?.Report(ChatStreamEvent.Final(errorMessage));
                return Fail(request, errorMessage, "LLM_TIMEOUT");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "LLM server error: {Message}", ex.Message);
                var errorMessage = "Xin lỗi, không thể kết nối đến máy chủ AI. (Cannot connect to AI server.)";
                request.StreamObserver?.Report(ChatStreamEvent.Final(errorMessage));
                return Fail(request, errorMessage, "LLM_ERROR");
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                // TaskCanceledException when ct is not cancelled means HTTP timeout
                _logger.LogError(ex, "LLM request timed out.");
                var errorMessage = "Xin lỗi, yêu cầu AI bị hết thời gian. Vui lòng thử lại. (AI request timed out. Please try again.)";
                request.StreamObserver?.Report(ChatStreamEvent.Final(errorMessage));
                return Fail(request, errorMessage, "LLM_TIMEOUT");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // User cancelled the request
                _logger.LogInformation("LLM request was cancelled by user.");
                var errorMessage = "Yêu cầu đã bị hủy. (Request was cancelled.)";
                request.StreamObserver?.Report(ChatStreamEvent.Final(errorMessage));
                return Fail(request, errorMessage, "REQUEST_CANCELLED");
            }

            llmStopwatch.Stop();
            _logger.LogInformation(
                "LlmRequestCompleted | Step: {Step} | Duration: {DurationMs}ms | HasToolCalls: {HasToolCalls} | ToolCallCount: {ToolCallCount}",
                step, llmStopwatch.ElapsedMilliseconds, response.ToolCalls.Count > 0, response.ToolCalls.Count);

            if (response.ToolCalls.Count == 0)
            {
                var content = response.Content ?? string.Empty;
                var assistantMessage = new ChatMessage(ChatRoles.Assistant, content);
                await _conversationStore.SaveAssistantMessageAsync(ctx, assistantMessage, policy, ct);
                request.StreamObserver?.Report(ChatStreamEvent.Final(content));

                if (request.AllowCache && _semanticCache.Enabled && !policy.ShouldBypassCache)
                {
                    await _semanticCache.SetAnswerAsync(
                        ctx,
                        "chat",
                        normalized,
                        tools,
                        null,
                        content,
                        containsSensitive,
                        ct);
                }

                pipelineStopwatch.Stop();
                _logger.LogInformation(
                    "PipelineCompleted | ConversationId: {ConversationId} | Duration: {DurationMs}ms | Steps: {Steps} | Success: true",
                    ctx.ConversationId, pipelineStopwatch.ElapsedMilliseconds, step + 1);

                return ChatResult.Ok(content);
            }

            if (!_recursionPolicy.TryAdvance(out var recursionError))
            {
                return Fail(request, recursionError);
            }

            if (response.ToolCalls.Count > _chatOptions.MaxToolCallsPerRequest)
            {
                return Fail(request, $"Tool call count exceeds MaxToolCallsPerRequest ({_chatOptions.MaxToolCallsPerRequest}).");
            }

            foreach (var call in response.ToolCalls)
            {
                request.StreamObserver?.Report(ChatStreamEvent.ToolCall(call));

                var validation = _toolGovernance.Validate(call, toolLookup, ctx);
                if (!validation.IsValid || validation.Tool is null)
                {
                    if (string.Equals(validation.Code, ErrorCode.ToolArgsInvalid, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new TilsoftApiException(
                            ErrorCode.ToolArgsInvalid,
                            400,
                            detail: validation.Detail);
                    }

                    return Fail(
                        request,
                        validation.Error ?? "Tool validation failed.",
                        validation.Code ?? ErrorCode.ToolValidationFailed,
                        validation.Detail);
                }

                var tool = validation.Tool;
                // Use sanitized arguments if available, otherwise fall back to original
                var argumentsJson = validation.SanitizedArgumentsJson ?? call.ArgumentsJson;
                var toolStopwatch = Stopwatch.StartNew();
                string rawResult;

                _logger.LogDebug(
                    "ToolExecutionStarting | ToolName: {ToolName} | SpName: {SpName}",
                    tool.Name, tool.SpName);

                try
                {
                    rawResult = await _toolHandler.ExecuteAsync(tool, argumentsJson, ctx, ct);
                }
                catch (Exception ex)
                {
                    toolStopwatch.Stop();
                    _logger.LogError(ex,
                        "ToolExecutionFailed | ToolName: {ToolName} | Duration: {DurationMs}ms",
                        tool.Name, toolStopwatch.ElapsedMilliseconds);
                    
                    _metrics.IncrementCounter(MetricNames.ToolExecutionsTotal, new Dictionary<string, string> { { "tool", tool.Name }, { "status", "failure" } });
                    
                    return Fail(request, $"Tool '{tool.Name}' execution failed.");
                }
                finally
                {
                    toolStopwatch.Stop();
                }

                _logger.LogInformation(
                    "ToolExecuted | ToolName: {ToolName} | Duration: {DurationMs}ms | ResultLength: {ResultLength}",
                    tool.Name, toolStopwatch.ElapsedMilliseconds, rawResult?.Length ?? 0);

                _metrics.IncrementCounter(MetricNames.ToolExecutionsTotal, new Dictionary<string, string> { { "tool", tool.Name }, { "status", "success" } });

                var maxBytes = _chatOptions.CompactionLimits.TryGetValue("ToolResultMaxBytes", out var limit) && limit > 0
                    ? limit
                    : 16000;
                var compacted = _toolResultCompactor.CompactJson(rawResult, maxBytes, _chatOptions.CompactionRules);
                var executionRecord = new ToolExecutionRecord
                {
                    ToolName = tool.Name,
                    SpName = tool.SpName,
                    ArgumentsJson = argumentsJson,
                    Result = rawResult,
                    CompactedResult = compacted,
                    Success = true,
                    DurationMs = toolStopwatch.ElapsedMilliseconds
                };

                await _conversationStore.SaveToolExecutionAsync(ctx, executionRecord, policy, ct);
                request.StreamObserver?.Report(ChatStreamEvent.ToolResult(executionRecord));

                messages.Add(new LlmMessage(ChatRoles.Tool, compacted, tool.Name));
            }
        }

        return Fail(request, "Max steps reached without final response.");
    }

    private async Task<LlmResponse> CompleteViaStreamAsync(LlmRequest request, ChatRequest chatRequest, CancellationToken ct)
    {
        var response = new LlmResponse();
        var contentBuilder = new System.Text.StringBuilder();

        await foreach (var evt in _llmClient.StreamAsync(request, ct))
        {
            switch (evt.Type)
            {
                case "delta":
                    if (!string.IsNullOrEmpty(evt.Content))
                    {
                        contentBuilder.Append(evt.Content);
                        chatRequest.StreamObserver?.Report(ChatStreamEvent.Delta(evt.Content));
                    }
                    break;
                case "tool_call":
                    if (evt.ToolCall is not null)
                    {
                        response.ToolCalls.Add(evt.ToolCall);
                    }
                    break;
                case "final":
                    if (!string.IsNullOrEmpty(evt.Content))
                    {
                        contentBuilder.Append(evt.Content);
                    }
                    break;
                case "error":
                    if (!string.IsNullOrEmpty(evt.Error))
                    {
                        // Wrap LLM errors in ErrorEnvelope to prevent leaking internal details
                        var errorEnvelope = new ErrorEnvelope
                        {
                            Code = ErrorCode.ChatFailed,
                            Detail = null // Never expose raw LLM error messages
                        };
                        chatRequest.StreamObserver?.Report(ChatStreamEvent.Error(errorEnvelope));
                    }
                    break;
            }
        }

        response.Content = contentBuilder.ToString();
        return response;
    }

    private ChatResult Fail(ChatRequest request, string errorMessage, string? code = null, object? detail = null)
    {
        // Emit structured ErrorEnvelope to stream, never raw error strings
        var errorEnvelope = new ErrorEnvelope
        {
            Code = code ?? ErrorCode.ChatFailed,
            Detail = detail // Pass structured detail only, not raw message
        };
        request.StreamObserver?.Report(ChatStreamEvent.Error(errorEnvelope));
        return ChatResult.Fail(errorMessage, code, detail);
    }
}
