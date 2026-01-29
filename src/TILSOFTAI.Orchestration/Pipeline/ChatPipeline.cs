using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Caching;
using TILSOFTAI.Orchestration.Compaction;
using TILSOFTAI.Orchestration.Conversations;
using TILSOFTAI.Orchestration.Llm;
using TILSOFTAI.Orchestration.Normalization;
using TILSOFTAI.Orchestration.Observability;
using TILSOFTAI.Orchestration.Policies;
using TILSOFTAI.Orchestration.Prompting;
using TILSOFTAI.Orchestration.Tools;

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
    private readonly ChatOptions _chatOptions;
    private readonly ILogRedactor _logRedactor;
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
        IOptions<ChatOptions> chatOptions,
        ILogRedactor logRedactor,
        ILogger<ChatPipeline> logger)
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
        _chatOptions = chatOptions?.Value ?? throw new ArgumentNullException(nameof(chatOptions));
        _logRedactor = logRedactor ?? throw new ArgumentNullException(nameof(logRedactor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ChatResult> RunAsync(ChatRequest request, TilsoftExecutionContext ctx, CancellationToken ct)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        _recursionPolicy.Reset();
        var normalized = await _normalizationService.NormalizeAsync(request.Input, ctx, ct);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Fail(request, "Input is empty.");
        }

        var userMessage = new ChatMessage(ChatRoles.User, normalized);
        await _conversationStore.SaveUserMessageAsync(ctx, userMessage, ct);

        var messages = new List<LlmMessage>
        {
            new(ChatRoles.User, normalized)
        };

        var tools = await _toolCatalogResolver.GetResolvedToolsAsync(ct);
        var toolLookup = tools.ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);

        // Compute sensitive flag server-side
        var (_, sensitiveFound) = _logRedactor.RedactText(normalized);
        var containsSensitive = sensitiveFound; 

        if (request.AllowCache && _semanticCache.Enabled)
        {
            var cached = await _semanticCache.TryGetAnswerAsync(
                ctx,
                "chat",
                normalized,
                tools,
                null,
                containsSensitive,
                ct);

            if (!string.IsNullOrWhiteSpace(cached))
            {
                request.StreamObserver?.Report(ChatStreamEvent.Final(cached));
                return ChatResult.Ok(cached);
            }
        }

        for (var step = 0; step < _chatOptions.MaxSteps; step++)
        {
            var llmRequest = await _promptBuilder.BuildAsync(messages, tools, ctx, ct);
            llmRequest.Stream = request.Stream;

            var response = request.Stream
                ? await CompleteViaStreamAsync(llmRequest, request, ct)
                : await _llmClient.CompleteAsync(llmRequest, ct);

            if (response.ToolCalls.Count == 0)
            {
                var content = response.Content ?? string.Empty;
                var assistantMessage = new ChatMessage(ChatRoles.Assistant, content);
                await _conversationStore.SaveAssistantMessageAsync(ctx, assistantMessage, ct);
                request.StreamObserver?.Report(ChatStreamEvent.Final(content));

                if (request.AllowCache && _semanticCache.Enabled)
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
                    return Fail(request, validation.Error ?? "Tool validation failed.");
                }

                var tool = validation.Tool;
                var stopwatch = Stopwatch.StartNew();
                string rawResult;
                try
                {
                    rawResult = await _toolHandler.ExecuteAsync(tool, call.ArgumentsJson, ctx, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tool execution failed for {ToolName}.", tool.Name);
                    return Fail(request, $"Tool '{tool.Name}' execution failed.");
                }
                finally
                {
                    stopwatch.Stop();
                }

                var maxBytes = _chatOptions.CompactionLimits.TryGetValue("ToolResultMaxBytes", out var limit) && limit > 0
                    ? limit
                    : 16000;
                var compacted = _toolResultCompactor.CompactJson(rawResult, maxBytes, _chatOptions.CompactionRules);
                var executionRecord = new ToolExecutionRecord
                {
                    ToolName = tool.Name,
                    SpName = tool.SpName,
                    ArgumentsJson = call.ArgumentsJson,
                    Result = rawResult,
                    CompactedResult = compacted,
                    Success = true,
                    DurationMs = stopwatch.ElapsedMilliseconds
                };

                await _conversationStore.SaveToolExecutionAsync(ctx, executionRecord, ct);
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
                        chatRequest.StreamObserver?.Report(ChatStreamEvent.Error(evt.Error));
                    }
                    break;
            }
        }

        response.Content = contentBuilder.ToString();
        return response;
    }

    private ChatResult Fail(ChatRequest request, string error)
    {
        request.StreamObserver?.Report(ChatStreamEvent.Error(error));
        return ChatResult.Fail(error);
    }
}
