using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Approvals;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.AiRouting.Tools;
using TILSOFTAI.Orchestration.Execution;
using TILSOFTAI.Orchestration.Observability;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;

public sealed class OfficialAgentToolRouter : IOfficialAgentToolRouter
{
    private readonly IHardSignalExtractor _hardSignalExtractor;
    private readonly ICapabilityCandidateSelector _candidateSelector;
    private readonly IOfficialAgentFunctionProvider _functionProvider;
    private readonly AgentRunOptionsFactory _agentRunOptionsFactory;
    private readonly IOfficialMicrosoftAgentRuntime _agentRuntime;
    private readonly IAnswerComposer _answerComposer;
    private readonly IToolRoutingTraceStore _traceStore;
    private readonly ICapabilityExecutionFacade? _executionFacade;
    private readonly IApprovalEngine? _approvalEngine;
    private readonly AiRoutingOptions _options;
    private readonly IMetricsService? _metrics;
    private readonly ILogger<OfficialAgentToolRouter> _logger;

    public OfficialAgentToolRouter(
        IHardSignalExtractor hardSignalExtractor,
        ICapabilityCandidateSelector candidateSelector,
        IOfficialAgentFunctionProvider functionProvider,
        AgentRunOptionsFactory agentRunOptionsFactory,
        IOfficialMicrosoftAgentRuntime agentRuntime,
        IAnswerComposer answerComposer,
        IToolRoutingTraceStore traceStore,
        IEnumerable<ICapabilityExecutionFacade> executionFacades,
        IEnumerable<IApprovalEngine> approvalEngines,
        IOptions<AiRoutingOptions> options,
        IEnumerable<IMetricsService> metricsServices,
        ILogger<OfficialAgentToolRouter> logger)
    {
        _hardSignalExtractor = hardSignalExtractor ?? throw new ArgumentNullException(nameof(hardSignalExtractor));
        _candidateSelector = candidateSelector ?? throw new ArgumentNullException(nameof(candidateSelector));
        _functionProvider = functionProvider ?? throw new ArgumentNullException(nameof(functionProvider));
        _agentRunOptionsFactory = agentRunOptionsFactory ?? throw new ArgumentNullException(nameof(agentRunOptionsFactory));
        _agentRuntime = agentRuntime ?? throw new ArgumentNullException(nameof(agentRuntime));
        _answerComposer = answerComposer ?? throw new ArgumentNullException(nameof(answerComposer));
        _traceStore = traceStore ?? throw new ArgumentNullException(nameof(traceStore));
        _executionFacade = executionFacades?.FirstOrDefault();
        _approvalEngine = approvalEngines?.FirstOrDefault();
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _metrics = metricsServices?.FirstOrDefault();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentToolRoutingResult> TryRouteAsync(
        AgentToolRoutingRequest request,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var stageStartedAt = startedAt;
        var stageLatencyMs = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        HardSignalSet? hardSignals = null;
        CapabilityRetrievalResult? retrieval = null;
        IReadOnlyList<AIFunction>? tools = null;

        RecordRequest(request);
        _logger.LogInformation(
            "{EventName} | correlationId: {CorrelationId} | tenantId: {TenantId} | userId: {UserId} | conversationId: {ConversationId} | allowedDomains: {AllowedDomains} | answerMode: {AnswerMode} | fallbackUsed: false",
            AgentRoutingTraceEvents.RouteStarted,
            request.ExecutionContext.CorrelationId,
            request.ExecutionContext.TenantId,
            request.ExecutionContext.UserId,
            request.ExecutionContext.ConversationId,
            string.Join(",", _options.AllowedDomains ?? Array.Empty<string>()),
            request.RequestedAnswerMode);

        try
        {
            hardSignals = _hardSignalExtractor.Extract(
                request.Message,
                request.Locale,
                request.ExecutionContext);
            MarkStage(stageLatencyMs, "hard_signal_extraction", ref stageStartedAt);

            var confirmation = TryCreateConfirmationTurn(request);
            if (confirmation is not null)
            {
                return await HandleConfirmationTurnAsync(
                        request,
                        confirmation,
                        hardSignals,
                        stageLatencyMs,
                        startedAt,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var selectedCandidates = await _candidateSelector.SelectAsync(
                request.Message,
                hardSignals,
                request.ExecutionContext,
                request.Locale,
                cancellationToken);
            retrieval = ToRetrievalResult(selectedCandidates);
            MarkStage(stageLatencyMs, "semantic_retrieval", ref stageStartedAt);
            _metrics?.RecordGauge(MetricNames.AgentRoutingCandidateCount, retrieval.Capabilities.Count, BaseLabels(request));
            _logger.LogInformation(
                "{EventName} | correlationId: {CorrelationId} | tenantId: {TenantId} | userId: {UserId} | conversationId: {ConversationId} | candidate_count: {CandidateCount} | candidateCapabilityKeys: {CandidateCapabilityKeys} | durationMs: {DurationMs}",
                AgentRoutingTraceEvents.CandidateSelectionCompleted,
                request.ExecutionContext.CorrelationId,
                request.ExecutionContext.TenantId,
                request.ExecutionContext.UserId,
                request.ExecutionContext.ConversationId,
                retrieval.Capabilities.Count,
                string.Join(",", retrieval.Capabilities.Select(candidate => candidate.Metadata.CapabilityKey)),
                StageDuration(stageLatencyMs, "semantic_retrieval"));

            if (retrieval.Capabilities.Count == 0)
            {
                LogFailedClosed(request, "NO_CANDIDATE_CAPABILITIES", startedAt);
                await SaveNotHandledTraceAsync(
                    request,
                    hardSignals,
                    retrieval,
                    tools,
                    "NO_CANDIDATE_CAPABILITIES",
                    stageLatencyMs,
                    startedAt,
                    cancellationToken);
                RecordFailure(request, "no_candidate_capabilities", startedAt, stageLatencyMs);
                return new AgentToolRoutingResult
                {
                    Handled = false,
                    FailureReason = "No candidate capabilities found."
                };
            }

            tools = await _functionProvider.BuildFunctionsAsync(
                retrieval.Capabilities,
                request.ExecutionContext,
                request.Locale,
                cancellationToken);
            MarkStage(stageLatencyMs, "tool_building", ref stageStartedAt);
            RecordCandidateTools(request, tools.Count);
            _metrics?.RecordGauge(MetricNames.AgentRoutingAdvertisedToolCount, tools.Count, BaseLabels(request));
            _logger.LogInformation(
                "{EventName} | correlationId: {CorrelationId} | tenantId: {TenantId} | userId: {UserId} | conversationId: {ConversationId} | advertised_tool_count: {AdvertisedToolCount} | advertisedFunctionNames: {AdvertisedFunctionNames} | durationMs: {DurationMs}",
                AgentRoutingTraceEvents.ToolsAdvertised,
                request.ExecutionContext.CorrelationId,
                request.ExecutionContext.TenantId,
                request.ExecutionContext.UserId,
                request.ExecutionContext.ConversationId,
                tools.Count,
                string.Join(",", tools.Select(tool => tool.Name)),
                StageDuration(stageLatencyMs, "tool_building"));

            if (tools.Count == 0)
            {
                LogFailedClosed(request, "NO_ADVERTISED_TOOLS", startedAt);
                await SaveNotHandledTraceAsync(
                    request,
                    hardSignals,
                    retrieval,
                    tools,
                    "NO_ADVERTISED_TOOLS",
                    stageLatencyMs,
                    startedAt,
                    cancellationToken);
                RecordFailure(request, "no_advertised_tools", startedAt, stageLatencyMs);
                return new AgentToolRoutingResult
                {
                    Handled = false,
                    FailureReason = "No tools created from candidate capabilities."
                };
            }

            _logger.LogInformation(
                "OfficialAgentToolRouterCandidates | DomainCount: {DomainCount} | CapabilityCount: {CapabilityCount} | ToolCount: {ToolCount}",
                retrieval.Domains.Count,
                retrieval.Capabilities.Count,
                tools.Count);

            _logger.LogInformation(
                "{EventName} | correlationId: {CorrelationId} | tenantId: {TenantId} | userId: {UserId} | conversationId: {ConversationId} | advertisedFunctionNames: {AdvertisedFunctionNames}",
                AgentRoutingTraceEvents.AgentRunStarted,
                request.ExecutionContext.CorrelationId,
                request.ExecutionContext.TenantId,
                request.ExecutionContext.UserId,
                request.ExecutionContext.ConversationId,
                string.Join(",", tools.Select(tool => tool.Name)));

            var agentResult = await _agentRuntime.RunAsync(
                    new OfficialMicrosoftAgentRunRequest(
                        tools,
                        AgentInstructionsBuilder.Build(request, hardSignals, retrieval),
                        request.Message,
                        request.ExecutionContext,
                        _agentRunOptionsFactory.Create()),
                    cancellationToken)
                .ConfigureAwait(false);
            MarkStage(stageLatencyMs, "agent_tool_selection", ref stageStartedAt);
            _metrics?.RecordHistogram(MetricNames.AgentRoutingAgentDurationMs, StageDuration(stageLatencyMs, "agent_tool_selection"), BaseLabels(request));
            _logger.LogInformation(
                "{EventName} | correlationId: {CorrelationId} | tenantId: {TenantId} | userId: {UserId} | conversationId: {ConversationId} | selectedFunctionName: {SelectedFunctionName} | capabilityKey: {CapabilityKey} | argumentsMasked: {ArgumentsMasked} | rowCount: {RowCount} | durationMs: {DurationMs}",
                AgentRoutingTraceEvents.AgentToolInvoked,
                request.ExecutionContext.CorrelationId,
                request.ExecutionContext.TenantId,
                request.ExecutionContext.UserId,
                request.ExecutionContext.ConversationId,
                agentResult.SelectedToolName ?? "none",
                agentResult.SelectedCapabilityKey ?? agentResult.ToolResult?.CapabilityKey ?? "none",
                MaskArguments(agentResult.Arguments.ToJsonString()),
                agentResult.ToolResult?.RowCount ?? 0,
                StageDuration(stageLatencyMs, "agent_tool_selection"));

            var answerRequest = AgentToolCallResultMapper.ToAnswerComposerRequest(
                agentResult,
                request,
                retrieval);

            _logger.LogInformation(
                "{EventName} | correlationId: {CorrelationId} | tenantId: {TenantId} | userId: {UserId} | conversationId: {ConversationId} | answerMode: {AnswerMode} | capabilityKey: {CapabilityKey} | rowCount: {RowCount}",
                AgentRoutingTraceEvents.AnswerComposerStarted,
                request.ExecutionContext.CorrelationId,
                request.ExecutionContext.TenantId,
                request.ExecutionContext.UserId,
                request.ExecutionContext.ConversationId,
                request.RequestedAnswerMode,
                answerRequest.CapabilityKey,
                answerRequest.RowCount);
            var answer = await _answerComposer.ComposeAsync(answerRequest, cancellationToken);
            MarkStage(stageLatencyMs, "answer_composition", ref stageStartedAt);
            _metrics?.RecordHistogram(MetricNames.AgentRoutingAnswerComposerDurationMs, StageDuration(stageLatencyMs, "answer_composition"), BaseLabels(request));
            _metrics?.RecordGauge(MetricNames.AgentRoutingRowCount, answer.Provenance.RowCount, BaseLabels(request));
            if (string.Equals(answer.AnswerType, "follow_up", StringComparison.OrdinalIgnoreCase))
            {
                _metrics?.IncrementCounter(MetricNames.AgentRoutingFollowUpTotal, BaseLabels(request));
            }
            if (string.Equals(answerRequest.ErrorCode, CapabilityExecutionFacade.ArgumentValidationFailedCode, StringComparison.OrdinalIgnoreCase))
            {
                _metrics?.IncrementCounter(MetricNames.AgentRoutingValidationFailureTotal, BaseLabels(request));
            }
            _logger.LogInformation(
                "{EventName} | correlationId: {CorrelationId} | tenantId: {TenantId} | userId: {UserId} | conversationId: {ConversationId} | answerMode: {AnswerMode} | answerType: {AnswerType} | capabilityKey: {CapabilityKey} | rowCount: {RowCount} | durationMs: {DurationMs}",
                AgentRoutingTraceEvents.AnswerComposerCompleted,
                request.ExecutionContext.CorrelationId,
                request.ExecutionContext.TenantId,
                request.ExecutionContext.UserId,
                request.ExecutionContext.ConversationId,
                request.RequestedAnswerMode,
                answer.AnswerType,
                answer.Provenance.CapabilityKey,
                answer.Provenance.RowCount,
                StageDuration(stageLatencyMs, "answer_composition"));

            await _traceStore.SaveAsync(
                ToolRoutingTraceFactory.FromSuccess(request, hardSignals, retrieval, tools, agentResult, answer, stageLatencyMs, startedAt),
                cancellationToken);

            RecordHandled(request, agentResult, startedAt, stageLatencyMs);

            return new AgentToolRoutingResult
            {
                Handled = true,
                Answer = answer
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "{EventName} | correlationId: {CorrelationId} | tenantId: {TenantId} | userId: {UserId} | conversationId: {ConversationId} | errorCode: {ErrorCode} | durationMs: {DurationMs} | fallbackUsed: false",
                AgentRoutingTraceEvents.RouteFailedClosed,
                request.ExecutionContext.CorrelationId,
                request.ExecutionContext.TenantId,
                request.ExecutionContext.UserId,
                request.ExecutionContext.ConversationId,
                ex.GetType().Name,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            try
            {
                await _traceStore.SaveAsync(
                    ToolRoutingTraceFactory.FromFailure(request, ex, startedAt),
                    CancellationToken.None);
            }
            catch (Exception traceEx)
            {
                _logger.LogWarning(traceEx, "OfficialAgentToolRouterTraceFailed");
            }

            RecordFailure(request, ex.GetType().Name, startedAt, stageLatencyMs);

            return new AgentToolRoutingResult
            {
                Handled = false,
                FailureReason = ex.Message
            };
        }
    }

    private void LogFailedClosed(AgentToolRoutingRequest request, string errorCode, long startedAt)
    {
        _logger.LogWarning(
            "{EventName} | correlationId: {CorrelationId} | tenantId: {TenantId} | userId: {UserId} | conversationId: {ConversationId} | errorCode: {ErrorCode} | durationMs: {DurationMs} | fallbackUsed: false",
            AgentRoutingTraceEvents.RouteFailedClosed,
            request.ExecutionContext.CorrelationId,
            request.ExecutionContext.TenantId,
            request.ExecutionContext.UserId,
            request.ExecutionContext.ConversationId,
            errorCode,
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
    }

    private async Task SaveNotHandledTraceAsync(
        AgentToolRoutingRequest request,
        HardSignalSet? hardSignals,
        CapabilityRetrievalResult? retrieval,
        IReadOnlyList<AIFunction>? tools,
        string reason,
        IReadOnlyDictionary<string, double> stageLatencyMs,
        long startedAt,
        CancellationToken cancellationToken)
    {
        try
        {
            await _traceStore.SaveAsync(
                    ToolRoutingTraceFactory.FromNotHandled(
                        request,
                        hardSignals,
                        retrieval,
                        tools,
                        reason,
                        stageLatencyMs,
                        startedAt),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "OfficialAgentToolRouterTraceFailed");
        }
    }

    private static void MarkStage(
        IDictionary<string, double> stageLatencyMs,
        string stage,
        ref long stageStartedAt)
    {
        stageLatencyMs[stage] = Stopwatch.GetElapsedTime(stageStartedAt).TotalMilliseconds;
        stageStartedAt = Stopwatch.GetTimestamp();
    }

    private void RecordRequest(AgentToolRoutingRequest request) =>
        _metrics?.IncrementCounter(
            MetricNames.AgentRoutingRequestsTotal,
            BaseLabels(request));

    private void RecordCandidateTools(AgentToolRoutingRequest request, int toolCount) =>
        _metrics?.RecordGauge(
            MetricNames.AgentRoutingCandidateTools,
            toolCount,
            BaseLabels(request));

    private void RecordHandled(
        AgentToolRoutingRequest request,
        AgentRunResult agentResult,
        long startedAt,
        IReadOnlyDictionary<string, double> stageLatencyMs)
    {
        var labels = BaseLabels(request);
        labels["capability"] = NormalizeLabel(agentResult.SelectedCapabilityKey);
        labels["status"] = string.IsNullOrWhiteSpace(agentResult.ClarificationQuestion) ? "handled" : "clarification";
        _metrics?.IncrementCounter(MetricNames.AgentRoutingHandledTotal, labels);

        if (!string.IsNullOrWhiteSpace(agentResult.ClarificationQuestion))
        {
            _metrics?.IncrementCounter(MetricNames.AgentRoutingClarificationsTotal, BaseLabels(request));
        }

        RecordLatency(request, "success", startedAt, stageLatencyMs);
    }

    private void RecordFailure(
        AgentToolRoutingRequest request,
        string reason,
        long startedAt,
        IReadOnlyDictionary<string, double> stageLatencyMs)
    {
        var labels = BaseLabels(request);
        labels["reason"] = NormalizeLabel(reason);
        _metrics?.IncrementCounter(MetricNames.AgentRoutingFailuresTotal, labels);
        RecordLatency(request, "failure", startedAt, stageLatencyMs);
    }

    private void RecordLatency(
        AgentToolRoutingRequest request,
        string outcome,
        long startedAt,
        IReadOnlyDictionary<string, double> stageLatencyMs)
    {
        var labels = BaseLabels(request);
        labels["outcome"] = outcome;
        _metrics?.RecordHistogram(
            MetricNames.AgentRoutingLatencySeconds,
            Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
            labels);

        foreach (var (stage, elapsedMs) in stageLatencyMs)
        {
            var stageLabels = BaseLabels(request);
            stageLabels["stage"] = stage;
            stageLabels["outcome"] = outcome;
            _metrics?.RecordHistogram(
                MetricNames.AgentRoutingStageLatencySeconds,
                elapsedMs / 1000d,
                stageLabels);
        }
    }

    private static double StageDuration(IReadOnlyDictionary<string, double> stageLatencyMs, string stage) =>
        stageLatencyMs.TryGetValue(stage, out var durationMs) ? durationMs : 0d;

    private static Dictionary<string, string> BaseLabels(AgentToolRoutingRequest request) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["locale"] = NormalizeLabel(request.Locale),
        ["answer_mode"] = NormalizeLabel(request.RequestedAnswerMode.ToString())
    };

    private static string NormalizeLabel(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant();

    private static string MaskArguments(string? argumentsJson) =>
        string.IsNullOrWhiteSpace(argumentsJson)
            ? "{}"
            : argumentsJson
                .Replace("password", "***", StringComparison.OrdinalIgnoreCase)
                .Replace("secret", "***", StringComparison.OrdinalIgnoreCase)
                .Replace("token", "***", StringComparison.OrdinalIgnoreCase);

    private async Task<AgentToolRoutingResult> HandleConfirmationTurnAsync(
        AgentToolRoutingRequest request,
        WriteConfirmationTurn confirmation,
        HardSignalSet hardSignals,
        Dictionary<string, double> stageLatencyMs,
        long startedAt,
        CancellationToken cancellationToken)
    {
        var stageStartedAt = Stopwatch.GetTimestamp();
        if (_approvalEngine is null)
        {
            return new AgentToolRoutingResult
            {
                Handled = false,
                FailureReason = "Write confirmation requires approval services."
            };
        }

        await _approvalEngine.ApproveAsync(
                confirmation.ApprovedActionId,
                ApprovalContext.FromExecutionContext(request.ExecutionContext, "microsoft-agent-router"),
                cancellationToken)
            .ConfigureAwait(false);
        MarkStage(stageLatencyMs, "write_confirmation_approval", ref stageStartedAt);

        var envelope = CapabilityExecutionEnvelope.Blocked(
            confirmation.CapabilityKey,
            "Pending action was confirmed for approval review; direct write execution is disabled.");

        var agentResult = new AgentRunResult
        {
            Outcome = AgentRunOutcome.ToolExecution,
            SelectedCapabilityKey = confirmation.CapabilityKey,
            Arguments = ToJsonObject(confirmation.Arguments),
            ToolResult = envelope
        };
        var retrieval = EmptyRetrieval();
        var answerRequest = AgentToolCallResultMapper.ToAnswerComposerRequest(
            agentResult,
            request,
            retrieval);
        var answer = await _answerComposer.ComposeAsync(answerRequest, cancellationToken)
            .ConfigureAwait(false);
        MarkStage(stageLatencyMs, "answer_composition", ref stageStartedAt);

        await _traceStore.SaveAsync(
                ToolRoutingTraceFactory.FromSuccess(
                    request,
                    hardSignals,
                    retrieval,
                    Array.Empty<AIFunction>(),
                    agentResult,
                    answer,
                    stageLatencyMs,
                    startedAt),
                cancellationToken)
            .ConfigureAwait(false);

        RecordHandled(request, agentResult, startedAt, stageLatencyMs);

        return new AgentToolRoutingResult
        {
            Handled = true,
            Answer = answer
        };
    }

    private static WriteConfirmationTurn? TryCreateConfirmationTurn(AgentToolRoutingRequest request)
    {
        var approvedActionId = ReadMetadataString(request.Metadata, "approvedActionId")
            ?? ReadMetadataString(request.Metadata, "actionId");
        if (string.IsNullOrWhiteSpace(approvedActionId))
        {
            return null;
        }

        var capabilityKey = ReadMetadataString(request.Metadata, "capabilityKey");
        if (string.IsNullOrWhiteSpace(capabilityKey))
        {
            return null;
        }

        return new WriteConfirmationTurn(
            capabilityKey,
            approvedActionId,
            ReadArguments(request.Metadata));
    }

    private static string? ReadMetadataString(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private static IReadOnlyDictionary<string, object?> ReadArguments(IReadOnlyDictionary<string, object?> metadata)
    {
        if (metadata.TryGetValue("arguments", out var arguments)
            && arguments is IReadOnlyDictionary<string, object?> objectArguments)
        {
            return new Dictionary<string, object?>(objectArguments, StringComparer.OrdinalIgnoreCase);
        }

        var argumentsJson = ReadMetadataString(metadata, "argumentsJson");
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson);
            return parsed is null
                ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object?>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static JsonObject ToJsonObject(IReadOnlyDictionary<string, object?> arguments) =>
        JsonSerializer.SerializeToNode(arguments)?.AsObject() ?? new JsonObject();

    private static CapabilityRetrievalResult EmptyRetrieval() => new()
    {
        Domains = Array.Empty<DomainCandidate>(),
        Capabilities = Array.Empty<CapabilityCandidate>(),
        EntityCandidates = Array.Empty<EntityCandidate>(),
        ContextChunks = Array.Empty<KnowledgeChunk>()
    };

    private static CapabilityRetrievalResult ToRetrievalResult(IReadOnlyList<CapabilityCandidate> candidates) => new()
    {
        Domains = candidates
            .GroupBy(candidate => candidate.Metadata.Domain, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DomainCandidate
            {
                Domain = group.Key,
                Score = group.Max(candidate => candidate.Score)
            })
            .OrderByDescending(domain => domain.Score)
            .ThenBy(domain => domain.Domain, StringComparer.OrdinalIgnoreCase)
            .ToArray(),
        Capabilities = candidates,
        EntityCandidates = Array.Empty<EntityCandidate>(),
        ContextChunks = Array.Empty<KnowledgeChunk>()
    };

    private sealed record WriteConfirmationTurn(
        string CapabilityKey,
        string ApprovedActionId,
        IReadOnlyDictionary<string, object?> Arguments);

}
