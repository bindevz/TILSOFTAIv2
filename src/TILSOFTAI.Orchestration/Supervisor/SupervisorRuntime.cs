using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.Actions;
using TILSOFTAI.Orchestration.AiRouting;

namespace TILSOFTAI.Supervisor;

public sealed class SupervisorRuntime : ISupervisorRuntime
{
    private readonly ILogger<SupervisorRuntime> _logger;
    private readonly IAgentToolRouter _agentToolRouter;
    private readonly AiRoutingOptions _aiRoutingOptions;
    private readonly IPendingActionConfirmationResolver? _pendingActionConfirmationResolver;

    public SupervisorRuntime(
        ILogger<SupervisorRuntime> logger,
        IAgentToolRouter agentToolRouter,
        IOptions<AiRoutingOptions>? aiRoutingOptions = null,
        IPendingActionConfirmationResolver? pendingActionConfirmationResolver = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _agentToolRouter = agentToolRouter ?? throw new ArgumentNullException(nameof(agentToolRouter));
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

        if (!IsAgentRoutingRolloutAllowed(ctx))
        {
            _logger.LogInformation(
                "AgentToolRoutingSkippedByRollout | TenantId: {TenantId} | UserId: {UserId}",
                string.IsNullOrWhiteSpace(ctx.TenantId) ? "unknown" : ctx.TenantId,
                string.IsNullOrWhiteSpace(ctx.UserId) ? "unknown" : ctx.UserId);

            return FailClosedAgentRouting(
                ctx,
                "Official Agent Framework routing is enabled, but this tenant or user is outside the rollout gate.",
                "AGENT_ROUTING_ROLLOUT_BLOCKED");
        }

        var routed = await TryRouteWithAgentToolRouterAsync(request, ctx, ct);
        if (routed.Handled && routed.Answer is not null)
        {
            _logger.LogInformation(
                "AgentToolRoutingHandled | AnswerMode: {AnswerMode}",
                ResolveAnswerMode(request));

            return SupervisorResult.FromAssistantAnswer(routed.Answer);
        }

        _logger.LogInformation(
            "AgentToolRoutingNotHandled | FailureReason: {FailureReason}",
            routed.FailureReason ?? "none");

        return FailClosedAgentRouting(
            ctx,
            routed.FailureReason ?? "Agent routing failed.",
            "AGENT_ROUTING_FAILED");
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

    private async Task<AgentToolRoutingResult> TryRouteWithAgentToolRouterAsync(
        SupervisorRequest request,
        TilsoftExecutionContext ctx,
        CancellationToken ct)
    {
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
