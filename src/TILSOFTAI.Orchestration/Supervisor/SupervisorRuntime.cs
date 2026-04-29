using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Agents.Abstractions;
using TILSOFTAI.Approvals;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.Actions;
using TILSOFTAI.Orchestration.AiRouting;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Orchestration.Observability;
using TILSOFTAI.Supervisor.Classification;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Supervisor;

public sealed class SupervisorRuntime : ISupervisorRuntime
{
    private readonly IIntentClassifier _intentClassifier;
    private readonly IAgentRegistry _agentRegistry;
    private readonly IApprovalEngine _approvalEngine;
    private readonly IToolAdapterRegistry _toolAdapterRegistry;
    private readonly ILogger<SupervisorRuntime> _logger;
    private readonly RuntimeExecutionInstrumentation? _instrumentation;
    private readonly IAgentToolRouter? _agentToolRouter;
    private readonly AiRoutingOptions _aiRoutingOptions;
    private readonly IPendingActionConfirmationResolver? _pendingActionConfirmationResolver;

    public SupervisorRuntime(
        IIntentClassifier intentClassifier,
        IAgentRegistry agentRegistry,
        IApprovalEngine approvalEngine,
        IToolAdapterRegistry toolAdapterRegistry,
        ILogger<SupervisorRuntime> logger,
        RuntimeExecutionInstrumentation? instrumentation = null,
        IAgentToolRouter? agentToolRouter = null,
        IOptions<AiRoutingOptions>? aiRoutingOptions = null,
        IPendingActionConfirmationResolver? pendingActionConfirmationResolver = null)
    {
        _intentClassifier = intentClassifier ?? throw new ArgumentNullException(nameof(intentClassifier));
        _agentRegistry = agentRegistry ?? throw new ArgumentNullException(nameof(agentRegistry));
        _approvalEngine = approvalEngine ?? throw new ArgumentNullException(nameof(approvalEngine));
        _toolAdapterRegistry = toolAdapterRegistry ?? throw new ArgumentNullException(nameof(toolAdapterRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _instrumentation = instrumentation;
        _agentToolRouter = agentToolRouter;
        _aiRoutingOptions = aiRoutingOptions?.Value ?? new AiRoutingOptions();
        _pendingActionConfirmationResolver = pendingActionConfirmationResolver;
    }

    public async Task<SupervisorResult> RunAsync(SupervisorRequest request, TilsoftExecutionContext ctx, CancellationToken ct)
    {
        if (request is null)
        {
            return SupervisorResult.Fail("Input is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Input))
        {
            return SupervisorResult.Fail("Input is required.");
        }

        if (_pendingActionConfirmationResolver is not null)
        {
            var confirmation = await _pendingActionConfirmationResolver
                .TryResolveAsync(request.Input, ctx, ct)
                .ConfigureAwait(false);

            if (confirmation?.Handled == true && confirmation.Answer is not null)
            {
                _logger.LogInformation(
                    "PendingActionConfirmationHandled | AnswerType: {AnswerType}",
                    confirmation.Answer.AnswerType);

                return SupervisorResult.FromAssistantAnswer(confirmation.Answer);
            }
        }

        if (IsOfficialAgentRoutingEnabled() && IsAgentRoutingRolloutAllowed(ctx))
        {
            var routed = await TryRouteWithAgentToolRouterAsync(request, ctx, ct);
            if (routed.Handled && routed.Answer is not null)
            {
                _logger.LogInformation(
                    "AgentToolRoutingHandled | AnswerMode: {AnswerMode}",
                    ResolveAnswerMode(request));

                return SupervisorResult.FromAssistantAnswer(routed.Answer);
            }

            _logger.LogInformation(
                "AgentToolRoutingNotHandled | FallbackToLegacyPipeline: {FallbackToLegacyPipeline} | FailureReason: {FailureReason}",
                _aiRoutingOptions.FallbackToLegacyPipeline,
                routed.FailureReason ?? "none");

            if (MustFailClosedForOfficialRouting() || !_aiRoutingOptions.FallbackToLegacyPipeline)
            {
                return FailClosedAgentRouting(
                    ctx,
                    routed.FailureReason ?? "Agent routing failed.",
                    "AGENT_ROUTING_FAILED");
            }
        }
        else if (IsOfficialAgentRoutingEnabled())
        {
            _logger.LogInformation(
                "AgentToolRoutingSkippedByRollout | TenantId: {TenantId} | UserId: {UserId}",
                string.IsNullOrWhiteSpace(ctx.TenantId) ? "unknown" : ctx.TenantId,
                string.IsNullOrWhiteSpace(ctx.UserId) ? "unknown" : ctx.UserId);

            if (MustFailClosedForOfficialRouting())
            {
                return FailClosedAgentRouting(
                    ctx,
                    "Official Agent Framework routing is enabled, but this tenant or user is outside the rollout gate.",
                    "AGENT_ROUTING_ROLLOUT_BLOCKED");
            }
        }

        var sw = Stopwatch.StartNew();

        // Step 1: Classify intent to determine domain hint (if not already provided)
        var task = MapRequest(request);
        IntentClassification? classificationResult = null;

        if (string.IsNullOrWhiteSpace(task.DomainHint))
        {
            var classification = await _intentClassifier.ClassifyAsync(request.Input, ct);
            classificationResult = classification;

            if (!string.IsNullOrWhiteSpace(classification.DomainHint))
            {
                task.DomainHint = classification.DomainHint;

                if (!string.IsNullOrWhiteSpace(classification.IntentType))
                {
                    task.IntentType = classification.IntentType;
                }

                // flag write intent for approval governance
                if (string.Equals(classification.IntentType, "write", StringComparison.OrdinalIgnoreCase))
                {
                    task.RequiresWritePreparation = true;
                    _logger.LogInformation(
                        "SupervisorWriteDetected | Domain: {Domain} | RequiresWritePreparation: true",
                        classification.DomainHint);
                }

                _logger.LogInformation(
                    "SupervisorClassified | Domain: {Domain} | Confidence: {Confidence} | Reasons: [{Reasons}]",
                    classification.DomainHint,
                    classification.Confidence,
                    string.Join("; ", classification.Reasons));
            }
            else
            {
                _logger.LogDebug(
                    "SupervisorClassified | Domain: unresolved | Reasons: [{Reasons}]",
                    string.Join("; ", classification.Reasons));
            }
        }

        // Build structured capability hint for domain agents
        task.CapabilityHint = BuildCapabilityHint(request, task, classificationResult);

        // Step 2: Resolve candidate agents
        var candidates = _agentRegistry.ResolveCandidates(task);
        if (candidates.Count == 0)
        {
            _logger.LogWarning(
                "SupervisorNoAgent | DomainHint: {DomainHint} | IntentType: {IntentType}",
                task.DomainHint ?? "none", task.IntentType);

            return SupervisorResult.Fail("No domain agent could handle the request.", "SUPERVISOR_AGENT_NOT_FOUND");
        }

        // Step 3: Select best agent from the registry's scored ordering.
        var selectedAgent = candidates[0];

        _logger.LogInformation(
            "SupervisorRouted | AgentId: {AgentId} | DomainHint: {DomainHint} | CandidateCount: {CandidateCount}",
            selectedAgent.AgentId,
            task.DomainHint ?? "unspecified",
            candidates.Count);

        // Step 4: Execute agent
        var result = await selectedAgent.ExecuteAsync(
            task,
            AgentExecutionContext.FromRuntimeContext(ctx, _approvalEngine, _toolAdapterRegistry),
            ct);

        sw.Stop();

        _logger.LogInformation(
            "SupervisorCompleted | AgentId: {AgentId} | Success: {Success} | DurationMs: {DurationMs}",
            selectedAgent.AgentId, result.Success, sw.ElapsedMilliseconds);

        _instrumentation?.RecordSupervisorExecution(
            selectedAgent.AgentId,
            task.DomainHint,
            sw.Elapsed,
            result.Success);

        return SupervisorResult.FromAgentResult(result, selectedAgent.AgentId);
    }

    public async IAsyncEnumerable<SupervisorStreamEvent> RunStreamAsync(
        SupervisorRequest request,
        TilsoftExecutionContext ctx,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (request is null)
        {
            yield return SupervisorStreamEvent.Error("Input is required.");
            yield break;
        }

        var channel = Channel.CreateUnbounded<SupervisorStreamEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = true
        });

        var terminalSeen = 0;
        var progress = new InlineProgress<SupervisorStreamEvent>(evt =>
        {
            if (Interlocked.CompareExchange(ref terminalSeen, 0, 0) == 1)
            {
                return;
            }

            channel.Writer.TryWrite(evt);

            if (IsTerminal(evt.Type))
            {
                Interlocked.Exchange(ref terminalSeen, 1);
            }
        });

        var streamingRequest = new SupervisorRequest
        {
            Input = request.Input,
            AllowCache = request.AllowCache,
            ContainsSensitive = request.ContainsSensitive,
            SensitivityReasons = request.SensitivityReasons,
            RequestPolicy = request.RequestPolicy,
            MessageHistory = request.MessageHistory,
            IntentType = request.IntentType,
            DomainHint = request.DomainHint,
            RequiresWritePreparation = request.RequiresWritePreparation,
            Stream = true,
            StreamObserver = progress,
            Metadata = request.Metadata
        };

        var runTask = Task.Run(async () =>
        {
            try
            {
                var result = await RunAsync(streamingRequest, ctx, ct);
                if (Interlocked.CompareExchange(ref terminalSeen, 1, 0) == 0)
                {
                    channel.Writer.TryWrite(result.Success
                        ? SupervisorStreamEvent.Final(result.Output ?? string.Empty)
                        : SupervisorStreamEvent.Error(result.Error ?? "Request failed."));
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Supervisor streaming execution failed.");
                if (Interlocked.CompareExchange(ref terminalSeen, 1, 0) == 0)
                {
                    channel.Writer.TryWrite(SupervisorStreamEvent.Error("Request failed."));
                }
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }, ct);

        await foreach (var evt in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return evt;

            if (IsTerminal(evt.Type))
            {
                break;
            }
        }

        try
        {
            await runTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private static AgentTask MapRequest(SupervisorRequest request) => new()
    {
        IntentType = request.IntentType ?? "chat",
        DomainHint = request.DomainHint,
        Input = request.Input,
        ContextPayload = request.Metadata,
        RequiresWritePreparation = request.RequiresWritePreparation,
        Stream = request.Stream,
        StreamObserver = request.StreamObserver,
        AllowCache = request.AllowCache,
        ContainsSensitive = request.ContainsSensitive,
        SensitivityReasons = request.SensitivityReasons,
        RequestPolicy = request.RequestPolicy,
        MessageHistory = request.MessageHistory
    };

    private async Task<AgentToolRoutingResult> TryRouteWithAgentToolRouterAsync(
        SupervisorRequest request,
        TilsoftExecutionContext ctx,
        CancellationToken ct)
    {
        if (_agentToolRouter is null)
        {
            _logger.LogWarning("AgentToolRoutingFailure | Reason: router_not_registered");
            return new AgentToolRoutingResult
            {
                Handled = false,
                FailureReason = "Agent routing is enabled, but no router is registered."
            };
        }

        var routingRequest = new AgentToolRoutingRequest
        {
            Message = request.Input,
            ExecutionContext = ctx,
            Locale = ResolveLocale(ctx),
            RequestedAnswerMode = ResolveAnswerMode(request),
            Metadata = ToObjectMetadata(request.Metadata)
        };

        _logger.LogInformation(
            "AgentToolRoutingAttempt | Locale: {Locale} | AnswerMode: {AnswerMode} | MaxTotalCandidateTools: {MaxTotalCandidateTools} | MaxToolCallsPerTurn: {MaxToolCallsPerTurn}",
            routingRequest.Locale,
            routingRequest.RequestedAnswerMode,
            _aiRoutingOptions.MaxTotalCandidateTools,
            _aiRoutingOptions.MaxToolCallsPerTurn);

        try
        {
            return await _agentToolRouter.TryRouteAsync(routingRequest, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AgentToolRoutingFailure | Reason: exception");
            return new AgentToolRoutingResult
            {
                Handled = false,
                FailureReason = "Agent routing failed."
            };
        }
    }

    private static string ResolveLocale(TilsoftExecutionContext ctx) =>
        string.IsNullOrWhiteSpace(ctx.Language) ? "vi-VN" : ctx.Language;

    private static AnswerMode ResolveAnswerMode(SupervisorRequest request)
    {
        if (request.Metadata.TryGetValue("answerMode", out var answerMode)
            && !string.IsNullOrWhiteSpace(answerMode)
            && (string.Equals(answerMode, "raw", StringComparison.OrdinalIgnoreCase)
                || string.Equals(answerMode, "rawJson", StringComparison.OrdinalIgnoreCase)
                || string.Equals(answerMode, "json", StringComparison.OrdinalIgnoreCase)))
        {
            return AnswerMode.RawJson;
        }

        return AnswerMode.Structured;
    }

    private static IReadOnlyDictionary<string, object?> ToObjectMetadata(IReadOnlyDictionary<string, string?> metadata)
    {
        if (metadata.Count == 0)
        {
            return new Dictionary<string, object?>();
        }

        return metadata.ToDictionary(
            pair => pair.Key,
            pair => (object?)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private bool IsAgentRoutingRolloutAllowed(TilsoftExecutionContext ctx)
    {
        var tenantGate = _aiRoutingOptions.EnabledTenantIds;
        var userGate = _aiRoutingOptions.EnabledUserIds;

        var tenantAllowed = tenantGate.Length == 0
            || tenantGate.Contains(ctx.TenantId, StringComparer.OrdinalIgnoreCase);
        var userAllowed = userGate.Length == 0
            || userGate.Contains(ctx.UserId, StringComparer.OrdinalIgnoreCase);

        return tenantAllowed && userAllowed;
    }

    private bool IsOfficialAgentRoutingEnabled() =>
        _aiRoutingOptions.MicrosoftAgentFrameworkRoutingEnabled
        || _aiRoutingOptions.UseOfficialMicrosoftAgentFramework;

    private bool MustFailClosedForOfficialRouting() =>
        _aiRoutingOptions.UseOfficialMicrosoftAgentFramework;

    private static SupervisorResult FailClosedAgentRouting(
        TilsoftExecutionContext ctx,
        string error,
        string code) =>
        SupervisorResult.Fail(
            error,
            code,
            new
            {
                correlationId = string.IsNullOrWhiteSpace(ctx.CorrelationId) ? null : ctx.CorrelationId,
                fallbackUsed = false
            },
            selectedAgentId: "microsoft-agent-router");

    /// <summary>
    /// Build a structured CapabilityRequestHint from request metadata and classification.
    /// Priority: explicit capabilityKey from metadata > domain + extracted keywords.
    /// </summary>
    private static CapabilityRequestHint BuildCapabilityHint(
        SupervisorRequest request,
        AgentTask task,
        IntentClassification? classification)
    {
        // If caller explicitly provided a capability key in metadata, use it directly
        string? explicitKey = null;
        if (request.Metadata.TryGetValue("capabilityKey", out var ck) && !string.IsNullOrWhiteSpace(ck))
        {
            explicitKey = ck;
        }

        // Extract subject keywords from input text
        var keywords = ExtractSubjectKeywords(request.Input);

        return new CapabilityRequestHint
        {
            CapabilityKey = explicitKey,
            Domain = task.DomainHint,
            Operation = task.IntentType,
            SubjectKeywords = keywords
        };
    }

    /// <summary>
    /// Extract meaningful subject keywords from user input.
    /// Filters out common stop words and short tokens.
    /// </summary>
    internal static IReadOnlyList<string> ExtractSubjectKeywords(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Array.Empty<string>();
        }

        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "show", "me", "the", "a", "an", "of", "in", "for", "to", "and", "or",
            "is", "are", "was", "were", "be", "been", "being", "get", "list",
            "what", "how", "where", "when", "who", "which", "please", "can", "could",
            "would", "should", "do", "does", "did", "will", "shall", "may", "might",
            "i", "you", "we", "they", "it", "my", "our", "your", "all", "this", "that",
            "with", "from", "on", "at", "by", "about", "give", "find", "have", "has",
            // Vietnamese stop words
            "cho", "tôi", "xem", "của", "và", "các", "là", "có", "được", "này",
            "đó", "từ", "trong", "nào", "bao", "nhiêu"
        };

        return input
            .Split(new[] { ' ', ',', '.', '?', '!', ':', ';', '-', '_', '/' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length >= 2 && !stopWords.Contains(w))
            .Select(w => w.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsTerminal(string? eventType) =>
        string.Equals(eventType, "final", StringComparison.OrdinalIgnoreCase)
        || string.Equals(eventType, "error", StringComparison.OrdinalIgnoreCase);

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public InlineProgress(Action<T> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void Report(T value) => _handler(value);
    }
}
