using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.AiRouting.Tools;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;

public sealed class MicrosoftAgentToolRouter : IAgentToolRouter
{
    private readonly IHardSignalExtractor _hardSignalExtractor;
    private readonly ISemanticCapabilityRetriever _retriever;
    private readonly IAgentFunctionToolFactory _toolFactory;
    private readonly IAgentClientFactory _agentClientFactory;
    private readonly IAnswerComposer _answerComposer;
    private readonly IToolRoutingTraceStore _traceStore;
    private readonly AiRoutingOptions _options;
    private readonly IMetricsService? _metrics;
    private readonly ILogger<MicrosoftAgentToolRouter> _logger;

    public MicrosoftAgentToolRouter(
        IHardSignalExtractor hardSignalExtractor,
        ISemanticCapabilityRetriever retriever,
        IAgentFunctionToolFactory toolFactory,
        IAgentClientFactory agentClientFactory,
        IAnswerComposer answerComposer,
        IToolRoutingTraceStore traceStore,
        IOptions<AiRoutingOptions> options,
        IEnumerable<IMetricsService> metricsServices,
        ILogger<MicrosoftAgentToolRouter> logger)
    {
        _hardSignalExtractor = hardSignalExtractor ?? throw new ArgumentNullException(nameof(hardSignalExtractor));
        _retriever = retriever ?? throw new ArgumentNullException(nameof(retriever));
        _toolFactory = toolFactory ?? throw new ArgumentNullException(nameof(toolFactory));
        _agentClientFactory = agentClientFactory ?? throw new ArgumentNullException(nameof(agentClientFactory));
        _answerComposer = answerComposer ?? throw new ArgumentNullException(nameof(answerComposer));
        _traceStore = traceStore ?? throw new ArgumentNullException(nameof(traceStore));
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
        IReadOnlyList<AgentFunctionTool>? tools = null;

        RecordRequest(request);

        try
        {
            hardSignals = _hardSignalExtractor.Extract(
                request.Message,
                request.Locale,
                request.ExecutionContext);
            MarkStage(stageLatencyMs, "hard_signal_extraction", ref stageStartedAt);

            retrieval = await _retriever.RetrieveAsync(
                request.Message,
                hardSignals,
                request.ExecutionContext,
                request.Locale,
                new CapabilityRetrievalOptions
                {
                    MaxDomainsPerRequest = _options.MaxCandidateDomains,
                    MaxToolsPerDomain = _options.MaxCandidateToolsPerDomain,
                    MaxTotalTools = _options.MaxTotalCandidateTools
                },
                cancellationToken);
            MarkStage(stageLatencyMs, "semantic_retrieval", ref stageStartedAt);

            if (retrieval.Capabilities.Count == 0)
            {
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

            tools = await _toolFactory.BuildToolsAsync(
                retrieval.Capabilities,
                request.ExecutionContext,
                request.Locale,
                cancellationToken);
            MarkStage(stageLatencyMs, "tool_building", ref stageStartedAt);
            RecordCandidateTools(request, tools.Count);

            if (tools.Count == 0)
            {
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
                "MicrosoftAgentToolRouterCandidates | DomainCount: {DomainCount} | CapabilityCount: {CapabilityCount} | ToolCount: {ToolCount}",
                retrieval.Domains.Count,
                retrieval.Capabilities.Count,
                tools.Count);

            var agent = _agentClientFactory.CreateToolCallingAgent(
                tools,
                AgentInstructionsBuilder.Build(request, hardSignals, retrieval));

            var agentResult = await agent.RunAsync(request.Message, cancellationToken);
            MarkStage(stageLatencyMs, "agent_tool_selection", ref stageStartedAt);

            var answerRequest = AgentToolCallResultMapper.ToAnswerComposerRequest(
                agentResult,
                request,
                retrieval);

            var answer = await _answerComposer.ComposeAsync(answerRequest, cancellationToken);
            MarkStage(stageLatencyMs, "answer_composition", ref stageStartedAt);

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
            _logger.LogWarning(ex, "MicrosoftAgentToolRouterFailed");

            try
            {
                await _traceStore.SaveAsync(
                    ToolRoutingTraceFactory.FromFailure(request, ex, startedAt),
                    CancellationToken.None);
            }
            catch (Exception traceEx)
            {
                _logger.LogWarning(traceEx, "MicrosoftAgentToolRouterTraceFailed");
            }

            RecordFailure(request, ex.GetType().Name, startedAt, stageLatencyMs);

            return new AgentToolRoutingResult
            {
                Handled = false,
                FailureReason = ex.Message
            };
        }
    }

    private async Task SaveNotHandledTraceAsync(
        AgentToolRoutingRequest request,
        HardSignalSet? hardSignals,
        CapabilityRetrievalResult? retrieval,
        IReadOnlyList<AgentFunctionTool>? tools,
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
            _logger.LogWarning(ex, "MicrosoftAgentToolRouterTraceFailed");
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

    private static Dictionary<string, string> BaseLabels(AgentToolRoutingRequest request) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["locale"] = NormalizeLabel(request.Locale),
        ["answer_mode"] = NormalizeLabel(request.RequestedAnswerMode.ToString())
    };

    private static string NormalizeLabel(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant();
}
