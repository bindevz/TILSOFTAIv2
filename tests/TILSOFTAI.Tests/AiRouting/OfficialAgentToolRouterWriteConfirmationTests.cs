using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TILSOFTAI.Approvals;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Orchestration.AiRouting;
using TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;
using TILSOFTAI.Orchestration.AiRouting.Tools;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.Answering.Narration;
using TILSOFTAI.Orchestration.Execution;
using TILSOFTAI.Orchestration.Semantic;
using Xunit;

namespace TILSOFTAI.Tests.AiRouting;

public sealed class OfficialAgentToolRouterWriteConfirmationTests
{
    [Fact]
    public async Task TryRouteAsync_WhenApprovedActionMetadataPresent_ApprovesWithoutExecutingWriteOrModel()
    {
        var approval = new RecordingApprovalEngine();
        var facade = new RecordingExecutionFacade();
        var agentRuntime = new ThrowingOfficialMicrosoftAgentRuntime();
        var router = new OfficialAgentToolRouter(
            new StubHardSignalExtractor(),
            new ThrowingCapabilityCandidateSelector(),
            new ThrowingToolFactory(),
            new AgentRunOptionsFactory(Options.Create(new AiRoutingOptions())),
            agentRuntime,
            new StructuredAnswerComposer(new RawJsonAnswerComposer(), new FallbackAnswerNarrationService()),
            new RecordingTraceStore(),
            [facade],
            [approval],
            Options.Create(new AiRoutingOptions { MicrosoftAgentFrameworkRoutingEnabled = true }),
            Array.Empty<IMetricsService>(),
            NullLogger<OfficialAgentToolRouter>.Instance);

        var result = await router.TryRouteAsync(
            new AgentToolRoutingRequest
            {
                Message = "confirm",
                ExecutionContext = new TilsoftExecutionContext
                {
                    TenantId = "tenant-32",
                    UserId = "user-32",
                    CorrelationId = Guid.NewGuid().ToString("N"),
                    Language = "en-US"
                },
                Locale = "en-US",
                RequestedAnswerMode = AnswerMode.Structured,
                Metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["approvedActionId"] = "action-32-6",
                    ["capabilityKey"] = "sales.order.create-execute",
                    ["argumentsJson"] = """{"customer_code":"C001"}"""
                }
            },
            CancellationToken.None);

        result.Handled.Should().BeTrue();
        result.Answer.Should().NotBeNull();
        result.Answer!.AnswerType.Should().Be("error");
        result.Answer.Text.Should().Contain("direct write execution is disabled");
        approval.ApprovedActionIds.Should().ContainSingle().Which.Should().Be("action-32-6");
        facade.ApprovedActionIds.Should().BeEmpty();
        facade.LastCapabilityKey.Should().BeNull();
        facade.LastArguments.Should().BeEmpty();
        agentRuntime.RunCalls.Should().Be(0);
    }

    private sealed class StubHardSignalExtractor : IHardSignalExtractor
    {
        public HardSignalSet Extract(string message, string locale, TilsoftExecutionContext context) => new();
    }

    private sealed class ThrowingCapabilityCandidateSelector : ICapabilityCandidateSelector
    {
        public Task<IReadOnlyList<CapabilityCandidate>> SelectAsync(
            string userMessage,
            HardSignalSet hardSignals,
            TilsoftExecutionContext context,
            string locale,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Confirmation turns should not run semantic retrieval.");
    }

    private sealed class ThrowingToolFactory : IOfficialAgentFunctionProvider
    {
        public Task<IReadOnlyList<AIFunction>> BuildFunctionsAsync(
            IReadOnlyList<CapabilityCandidate> candidates,
            TilsoftExecutionContext context,
            string locale,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Confirmation turns should not build model tools.");
    }

    private sealed class ThrowingOfficialMicrosoftAgentRuntime : IOfficialMicrosoftAgentRuntime
    {
        public int RunCalls { get; private set; }

        public Task<AgentRunResult> RunAsync(
            OfficialMicrosoftAgentRunRequest request,
            CancellationToken cancellationToken)
        {
            RunCalls++;
            throw new InvalidOperationException("Confirmation turns should not invoke the model.");
        }
    }

    private sealed class RecordingExecutionFacade : ICapabilityExecutionFacade
    {
        public List<string> ApprovedActionIds { get; } = [];
        public string? LastCapabilityKey { get; private set; }
        public IReadOnlyDictionary<string, object?> LastArguments { get; private set; } =
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        public Task<CapabilityExecutionEnvelope> ExecuteReadAsync(
            string capabilityKey,
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CapabilityExecutionEnvelope> PreviewWriteAsync(
            string capabilityKey,
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CapabilityExecutionEnvelope> ExecuteApprovedWriteAsync(
            string capabilityKey,
            string approvedActionId,
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken)
        {
            ApprovedActionIds.Add(approvedActionId);
            LastCapabilityKey = capabilityKey;
            LastArguments = arguments;
            return Task.FromResult(new CapabilityExecutionEnvelope
            {
                CapabilityKey = capabilityKey,
                ExecutionMode = "write_execute",
                ProcedureName = "dbo.sales_order_create",
                Arguments = arguments,
                Rows =
                [
                    new Dictionary<string, object?>
                    {
                        ["ActionId"] = approvedActionId,
                        ["Status"] = "Executed"
                    }
                ],
                RowCount = 1,
                ExecutionMetadata = new ExecutionMetadata
                {
                    Operation = "execute_write_action",
                    AdapterType = "sql",
                    CorrelationId = "corr-32-6"
                },
                SensitivityPolicy = SensitivityPolicy.Default,
                AnswerPolicy = AnswerPolicy.Default,
                Success = true,
                Status = "succeeded"
            });
        }
    }

    private sealed class RecordingApprovalEngine : IApprovalEngine
    {
        public List<string> ApprovedActionIds { get; } = [];

        public Task<ProposedActionRecord> CreateAsync(
            ProposedAction action,
            ApprovalContext context,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ProposedActionRecord> ApproveAsync(
            string actionId,
            ApprovalContext context,
            CancellationToken ct)
        {
            ApprovedActionIds.Add(actionId);
            return Task.FromResult(new ProposedActionRecord
            {
                ActionId = actionId,
                TenantId = context.TenantId,
                Status = "Approved",
                RequestedByUserId = context.UserId,
                ApprovedByUserId = context.UserId,
                ApprovedAtUtc = DateTime.UtcNow
            });
        }

        public Task<ProposedActionRecord> RejectAsync(
            string actionId,
            ApprovalContext context,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ActionExecutionResult> ExecuteAsync(
            string actionId,
            ApprovalContext context,
            CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingTraceStore : IToolRoutingTraceStore
    {
        public Task SaveAsync(ToolRoutingTrace trace, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FallbackAnswerNarrationService : IAnswerNarrationService
    {
        private readonly GenericSchemaSummaryFallback _fallback = new();

        public Task<AnswerNarrationResult> GenerateAsync(
            AnswerNarrationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_fallback.Generate(request));
    }
}
