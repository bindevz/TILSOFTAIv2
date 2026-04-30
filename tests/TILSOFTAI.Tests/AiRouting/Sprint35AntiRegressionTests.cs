using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.Answering.Narration;
using TILSOFTAI.Orchestration.AiRouting;
using TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;
using TILSOFTAI.Orchestration.AiRouting.Tools;
using TILSOFTAI.Orchestration.Execution;
using TILSOFTAI.Orchestration.Semantic;
using Xunit;

namespace TILSOFTAI.Tests.AiRouting;

public sealed class AgentFrameworkAntiRegressionTests
{
    [Fact]
    public async Task Router_ShouldLetOfficialRuntimeSelectMaterials_WhenWrongCandidateIsFirst()
    {
        var facade = new CountingCapabilityExecutionFacade();
        var runtime = new SelectingAgentRuntime(
            "model_get_materials",
            new Dictionary<string, object?> { ["modelCode"] = "ABC" });
        var router = CreateRouter(
            [
                ModelCandidate("model.overview.by-code"),
                ModelCandidate("model.materials.by-code")
            ],
            facade,
            runtime);

        var result = await router.TryRouteAsync(
            Request("Show materials for model ABC", AnswerMode.Structured),
            CancellationToken.None);

        result.Handled.Should().BeTrue();
        runtime.RunCount.Should().Be(1);
        runtime.LastRequest!.Functions.Select(function => function.Name)
            .Should()
            .Equal("model_get_overview", "model_get_materials");
        runtime.SelectedFunctionName.Should().Be("model_get_materials");
        facade.ExecuteReadCount.Should().Be(1);
        facade.LastCapabilityKey.Should().Be("model.materials.by-code");
        facade.LastArguments.Should().ContainKey("modelCode").WhoseValue.Should().Be("ABC");
        result.Answer!.Provenance.CapabilityKey.Should().Be("model.materials.by-code");
    }

    [Fact]
    public async Task Router_ShouldAskFollowUpAndNotExecuteFacade_WhenModelCodeIsMissing()
    {
        var facade = new CountingCapabilityExecutionFacade();
        var runtime = new SelectingAgentRuntime(
            selectedFunctionName: null,
            arguments: new Dictionary<string, object?>(),
            clarificationQuestion: "Which modelCode should I use?");
        var router = CreateRouter(
            [ModelCandidate("model.overview.by-code")],
            facade,
            runtime);

        var result = await router.TryRouteAsync(
            Request("Cho tôi xem thông tin model", AnswerMode.Structured, "vi-VN"),
            CancellationToken.None);

        result.Handled.Should().BeTrue();
        result.Answer!.AnswerType.Should().Be("follow_up");
        result.Answer.Text.Should().Contain("modelCode");
        facade.ExecuteReadCount.Should().Be(0);
    }

    [Fact]
    public async Task Router_RawJsonMode_ShouldExecuteToolOnceAndNotRunFinalLlmSummary()
    {
        var facade = new CountingCapabilityExecutionFacade();
        var runtime = new SelectingAgentRuntime(
            "model_get_overview",
            new Dictionary<string, object?> { ["modelCode"] = "ABC" });
        var router = CreateRouter(
            [ModelCandidate("model.overview.by-code")],
            facade,
            runtime);

        var result = await router.TryRouteAsync(
            Request("Cho tôi xem thông tin model ABC", AnswerMode.RawJson, "vi-VN"),
            CancellationToken.None);

        result.Handled.Should().BeTrue();
        runtime.RunCount.Should().Be(1);
        facade.ExecuteReadCount.Should().Be(1);
        result.Answer!.AnswerType.Should().Be("raw_json");
        result.Answer.Blocks.Should().ContainSingle().Which.Should().BeOfType<RawJsonBlock>();
    }

    [Fact]
    public async Task Router_StructuredMode_ShouldReturnBlocksAndProvenance()
    {
        var facade = new CountingCapabilityExecutionFacade();
        var runtime = new SelectingAgentRuntime(
            "model_get_overview",
            new Dictionary<string, object?> { ["modelCode"] = "ABC" });
        var router = CreateRouter(
            [ModelCandidate("model.overview.by-code")],
            facade,
            runtime);

        var result = await router.TryRouteAsync(
            Request("Show model ABC", AnswerMode.Structured),
            CancellationToken.None);

        result.Handled.Should().BeTrue();
        result.Answer!.Text.Should().NotBeNullOrWhiteSpace();
        result.Answer.AnswerType.Should().Be("structured");
        result.Answer.Blocks.Should().NotBeEmpty();
        result.Answer.Provenance.CapabilityKey.Should().Be("model.overview.by-code");
        result.Answer.Provenance.RowCount.Should().Be(1);
    }

    [Fact]
    public async Task Router_ShouldNotAdvertiseNonModelTools_WithModelOnlyConfiguration()
    {
        var facade = new CountingCapabilityExecutionFacade();
        var runtime = new SelectingAgentRuntime(
            "model_get_overview",
            new Dictionary<string, object?> { ["modelCode"] = "ABC" });
        var router = CreateRouter(
            [
                ModelCandidate("model.overview.by-code"),
                ModelCandidate("warehouse.stock.available", domain: "warehouse"),
                ModelCandidate("sales.order.status", domain: "sales"),
                ModelCandidate("accounting.receivables", domain: "accounting")
            ],
            facade,
            runtime);

        var result = await router.TryRouteAsync(
            Request("Show model ABC", AnswerMode.Structured),
            CancellationToken.None);

        result.Handled.Should().BeTrue();
        runtime.LastRequest!.Functions.Select(function => function.Name)
            .Should()
            .Equal("model_get_overview");
    }

    [Fact]
    public async Task Router_ShouldAdvertiseAtMostSixModelTools_WithMixedCandidates()
    {
        var facade = new CountingCapabilityExecutionFacade();
        var runtime = new SelectingAgentRuntime(
            "model_get_overview",
            new Dictionary<string, object?> { ["modelCode"] = "ABC" });
        var router = CreateRouter(
            [
                ModelCandidate("model.overview.by-code"),
                ModelCandidate("warehouse.stock.available", domain: "warehouse"),
                ModelCandidate("model.pieces.by-code"),
                ModelCandidate("sales.order.status", domain: "sales"),
                ModelCandidate("model.materials.by-code"),
                ModelCandidate("model.packaging.by-code"),
                ModelCandidate("model.count"),
                ModelCandidate("model.compare"),
                ModelCandidate("model.extra.one"),
                ModelCandidate("accounting.receivables", domain: "accounting")
            ],
            facade,
            runtime);

        var result = await router.TryRouteAsync(
            Request("Show model ABC", AnswerMode.Structured),
            CancellationToken.None);

        result.Handled.Should().BeTrue();
        runtime.LastRequest!.Functions.Should().HaveCount(6);
        runtime.LastRequest.Functions.Select(function => function.Name)
            .Should()
            .OnlyContain(name => name.StartsWith("model_", StringComparison.OrdinalIgnoreCase));
    }

    private static OfficialAgentToolRouter CreateRouter(
        IReadOnlyList<CapabilityCandidate> candidates,
        CountingCapabilityExecutionFacade facade,
        SelectingAgentRuntime runtime)
    {
        var options = Options.Create(new AiRoutingOptions
        {
            AllowedDomains = ["model"],
            MaxCandidateTools = 6,
            MaxTotalCandidateTools = 6,
            MaxCandidateToolsPerDomain = 6,
            MaxCandidateDomains = 1,
            FallbackToLegacyPipeline = false
        });
        var functionProvider = new DynamicFunctionToolFactory(
            new CapabilityToolDescriptorFactory(
                new CapabilityToolDescriptionBuilder(),
                new CapabilityParameterSchemaBuilder()),
            facade,
            new StubCompositeCapabilityExecutor(),
            options);

        return new OfficialAgentToolRouter(
            new StubHardSignalExtractor(),
            new StubCapabilityCandidateSelector(candidates),
            functionProvider,
            new AgentRunOptionsFactory(options),
            runtime,
            new StructuredAnswerComposer(new RawJsonAnswerComposer(), new FallbackAnswerNarrationService()),
            new StubToolRoutingTraceStore(),
            [facade],
            [],
            options,
            Array.Empty<IMetricsService>(),
            NullLogger<OfficialAgentToolRouter>.Instance);
    }

    private static AgentToolRoutingRequest Request(
        string message,
        AnswerMode mode,
        string locale = "en-US") => new()
        {
            Message = message,
            Locale = locale,
            RequestedAnswerMode = mode,
            ExecutionContext = new TilsoftExecutionContext
            {
                TenantId = "tenant-35",
                UserId = "user-35",
                ConversationId = "conversation-35",
                CorrelationId = "corr-35"
            }
        };

    private static CapabilityCandidate ModelCandidate(string key, string domain = "model") => new()
    {
        Score = 1,
        Metadata = new CapabilitySemanticMetadata
        {
            CapabilityKey = key,
            Domain = domain,
            FunctionName = FunctionNameFor(key),
            AdapterType = "sql",
            Operation = "read",
            ExecutionMode = "read",
            Text = new CapabilityTextMetadata
            {
                Locale = "en-US",
                Description = "Model read capability.",
                UseWhen = "Use for model questions.",
                DoNotUseWhen = "Do not use for other domains."
            },
            Arguments =
            [
                new CapabilityArgumentMetadata
                {
                    ArgumentName = "modelCode",
                    ProcParameterName = "@ModelCode",
                    DataType = "string",
                    IsRequired = true,
                    DisplayOrder = 1,
                    Text = new CapabilityArgumentTextMetadata
                    {
                        Locale = "en-US",
                        Description = "Business model code.",
                        Aliases = """["model code"]""",
                        Examples = """["ABC"]"""
                    }
                }
            ]
        }
    };

    private static string FunctionNameFor(string key) =>
        key switch
        {
            "model.overview.by-code" => "model_get_overview",
            "model.materials.by-code" => "model_get_materials",
            _ => key.Replace('.', '_').Replace('-', '_')
        };

    private sealed class SelectingAgentRuntime : IOfficialMicrosoftAgentRuntime
    {
        private readonly string? _selectedFunctionName;
        private readonly IReadOnlyDictionary<string, object?> _arguments;
        private readonly string? _clarificationQuestion;

        public SelectingAgentRuntime(
            string? selectedFunctionName,
            IReadOnlyDictionary<string, object?> arguments,
            string? clarificationQuestion = null)
        {
            _selectedFunctionName = selectedFunctionName;
            _arguments = arguments;
            _clarificationQuestion = clarificationQuestion;
        }

        public int RunCount { get; private set; }
        public string? SelectedFunctionName { get; private set; }
        public OfficialMicrosoftAgentRunRequest? LastRequest { get; private set; }

        public async Task<AgentRunResult> RunAsync(
            OfficialMicrosoftAgentRunRequest request,
            CancellationToken cancellationToken)
        {
            RunCount++;
            LastRequest = request;

            if (!string.IsNullOrWhiteSpace(_clarificationQuestion))
            {
                return new AgentRunResult
                {
                    Outcome = AgentRunOutcome.Clarification,
                    ClarificationQuestion = _clarificationQuestion
                };
            }

            var function = request.Functions.Single(item => item.Name == _selectedFunctionName);
            SelectedFunctionName = function.Name;
            var result = await function.InvokeAsync(
                new AIFunctionArguments(new Dictionary<string, object?>(_arguments, StringComparer.OrdinalIgnoreCase)),
                cancellationToken);
            var invocation = ((ICapabilityBackedAIFunction)function).LastInvocation!;

            return new AgentRunResult
            {
                Outcome = AgentRunOutcome.ToolExecution,
                SelectedToolName = function.Name,
                SelectedCapabilityKey = invocation.Descriptor.Capability.CapabilityKey,
                Arguments = JsonSerializer.SerializeToNode(_arguments)!.AsObject(),
                ToolResult = (CapabilityExecutionEnvelope)result!
            };
        }
    }

    private sealed class CountingCapabilityExecutionFacade : ICapabilityExecutionFacade
    {
        public int ExecuteReadCount { get; private set; }
        public string? LastCapabilityKey { get; private set; }
        public IReadOnlyDictionary<string, object?> LastArguments { get; private set; } =
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        public Task<CapabilityExecutionEnvelope> ExecuteReadAsync(
            string capabilityKey,
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken)
        {
            ExecuteReadCount++;
            LastCapabilityKey = capabilityKey;
            LastArguments = arguments;

            return Task.FromResult(new CapabilityExecutionEnvelope
            {
                CapabilityKey = capabilityKey,
                ExecutionMode = "read",
                ProcedureName = "dbo.ai_model_read",
                Arguments = arguments,
                Rows =
                [
                    new Dictionary<string, object?>
                    {
                        ["ModelCode"] = arguments.TryGetValue("modelCode", out var modelCode) ? modelCode : "ABC",
                        ["ModelName"] = "Chair"
                    }
                ],
                RowCount = 1,
                ExecutionMetadata = new ExecutionMetadata
                {
                    CorrelationId = "corr-35",
                    Operation = "execute_query"
                },
                SensitivityPolicy = SensitivityPolicy.Default,
                AnswerPolicy = AnswerPolicy.Default,
                Success = true,
                Status = "succeeded"
            });
        }

        public Task<CapabilityExecutionEnvelope> PreviewWriteAsync(
            string capabilityKey,
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken) =>
            Task.FromResult(CapabilityExecutionEnvelope.Blocked(capabilityKey, "write preview disabled"));

        public Task<CapabilityExecutionEnvelope> ExecuteApprovedWriteAsync(
            string capabilityKey,
            string approvedActionId,
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken) =>
            Task.FromResult(CapabilityExecutionEnvelope.Blocked(capabilityKey, "write execution disabled"));
    }

    private sealed class StubCapabilityCandidateSelector : ICapabilityCandidateSelector
    {
        private readonly IReadOnlyList<CapabilityCandidate> _candidates;

        public StubCapabilityCandidateSelector(IReadOnlyList<CapabilityCandidate> candidates)
        {
            _candidates = candidates;
        }

        public Task<IReadOnlyList<CapabilityCandidate>> SelectAsync(
            string userMessage,
            HardSignalSet hardSignals,
            TilsoftExecutionContext context,
            string locale,
            CancellationToken cancellationToken) =>
            Task.FromResult(_candidates);
    }

    private sealed class StubHardSignalExtractor : IHardSignalExtractor
    {
        public HardSignalSet Extract(string message, string locale, TilsoftExecutionContext context) => new();
    }

    private sealed class StubToolRoutingTraceStore : IToolRoutingTraceStore
    {
        public Task SaveAsync(ToolRoutingTrace trace, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubCompositeCapabilityExecutor : ICompositeCapabilityExecutor
    {
        public Task<CapabilityExecutionEnvelope> ExecuteAsync(
            string compositeCapabilityKey,
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken) =>
            Task.FromResult(CapabilityExecutionEnvelope.Blocked(compositeCapabilityKey, "composite disabled"));
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
