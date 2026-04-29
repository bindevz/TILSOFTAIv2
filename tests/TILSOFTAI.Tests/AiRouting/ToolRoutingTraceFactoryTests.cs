using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.AI;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.AiRouting;
using TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;
using TILSOFTAI.Orchestration.AiRouting.Tools;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.Execution;
using TILSOFTAI.Orchestration.Semantic;
using Xunit;

namespace TILSOFTAI.Tests.AiRouting;

public sealed class ToolRoutingTraceFactoryTests
{
    [Fact]
    public void FromSuccess_ShouldCapturePhase8TelemetryFields()
    {
        var request = new AgentToolRoutingRequest
        {
            Message = "Show inventory for MADEIRA-BLK",
            Locale = "en-US",
            ExecutionContext = new TilsoftExecutionContext
            {
                TenantId = "tenant-1",
                UserId = "user-1",
                CorrelationId = Guid.NewGuid().ToString(),
                ConversationId = "conv-1"
            },
            RequestedAnswerMode = AnswerMode.Structured,
            Metadata = new Dictionary<string, object?> { ["model"] = "gpt-test" }
        };
        var hardSignals = new HardSignalSet
        {
            Codes = [new CodeSignal { Text = "MADEIRA-BLK", PossibleType = "item" }]
        };
        var metadata = Metadata("warehouse.inventory.by-item");
        var retrieval = new CapabilityRetrievalResult
        {
            Domains = [new DomainCandidate { Domain = "warehouse", Score = 0.91 }],
            Capabilities = [new CapabilityCandidate { Metadata = metadata, Score = 0.91 }],
            EntityCandidates = [],
            ContextChunks = []
        };
        var tool = new TraceFunction(new CapabilityToolDescriptor
        {
            Name = "warehouse_inventory_by_item",
            Description = "Check inventory.",
            ParameterSchema = new JsonObject(),
            Capability = metadata,
            Arguments = []
        });
        var envelope = new CapabilityExecutionEnvelope
        {
            CapabilityKey = metadata.CapabilityKey,
            ExecutionMode = "read",
            Arguments = new Dictionary<string, object?> { ["@ItemNo"] = "MADEIRA-BLK" },
            RowCount = 1,
            Success = true,
            Status = "succeeded",
            ExecutionMetadata = new ExecutionMetadata { AdapterType = "sql", Operation = "execute_query" },
            SensitivityPolicy = SensitivityPolicy.Default,
            AnswerPolicy = AnswerPolicy.Default,
            ProcedureName = "dbo.ai_warehouse_inventory_by_item"
        };
        var agentResult = new AgentRunResult
        {
            SelectedToolName = "warehouse_inventory_by_item",
            SelectedCapabilityKey = metadata.CapabilityKey,
            Arguments = new JsonObject { ["item_no"] = "MADEIRA-BLK" },
            ToolResult = envelope
        };
        var answer = new AssistantAnswer
        {
            AnswerType = "structured",
            Text = "Inventory summary",
            Blocks = [new TextBlock("Inventory summary")],
            Provenance = new AnswerProvenance { CapabilityKey = metadata.CapabilityKey, RowCount = 1 }
        };

        var trace = ToolRoutingTraceFactory.FromSuccess(
            request,
            hardSignals,
            retrieval,
            [tool],
            agentResult,
            answer,
            new Dictionary<string, double> { ["semantic_retrieval"] = 12.5 },
            System.Diagnostics.Stopwatch.GetTimestamp());

        trace.HardSignalsJson.Should().Contain("MADEIRA-BLK");
        trace.AdvertisedFunctionToolsJson.Should().Contain("warehouse_inventory_by_item");
        trace.SelectedFunction.Should().Be("warehouse_inventory_by_item");
        trace.SelectedCapabilityKey.Should().Be("warehouse.inventory.by-item");
        trace.StoredProcedure.Should().Be("dbo.ai_warehouse_inventory_by_item");
        trace.CandidateDomainCount.Should().Be(1);
        trace.CandidateCapabilityCount.Should().Be(1);
        trace.AdvertisedToolCount.Should().Be(1);
        trace.ArgumentsBeforeNormalizationJson.Should().Contain("item_no");
        trace.ArgumentsAfterNormalizationJson.Should().Contain("@ItemNo");
        trace.AdapterType.Should().Be("sql");
        trace.RowCount.Should().Be(1);
        trace.LatencyByStageJson.Should().Contain("semantic_retrieval");
        trace.ModelProvider.Should().Be("microsoft-agent-framework");
        trace.ModelName.Should().Be("gpt-test");
        trace.AllowedDomainsJson.Should().Contain("warehouse");
        trace.ConversationId.Should().Be("conv-1");
        trace.FallbackUsed.Should().BeFalse();
    }

    [Fact]
    public void FromNotHandled_ShouldCaptureNoToolFollowUpDiagnostics()
    {
        var trace = ToolRoutingTraceFactory.FromNotHandled(
            Request(),
            new HardSignalSet(),
            Retrieval(),
            [],
            "follow_up_required",
            new Dictionary<string, double> { ["candidate_selection"] = 4 },
            System.Diagnostics.Stopwatch.GetTimestamp());

        trace.Success.Should().BeFalse();
        trace.ErrorCode.Should().Be("follow_up_required");
        trace.ValidationResultJson.Should().Contain("routed");
        trace.ValidationResultJson.Should().Contain("follow_up_required");
        trace.CandidateCapabilityCount.Should().Be(1);
        trace.AdvertisedToolCount.Should().Be(0);
        trace.FallbackUsed.Should().BeFalse();
    }

    [Fact]
    public void FromSuccess_ShouldCaptureValidationFailureDiagnostics()
    {
        var trace = ToolRoutingTraceFactory.FromSuccess(
            Request(),
            new HardSignalSet(),
            Retrieval(),
            [Tool(Metadata("warehouse.inventory.by-item"))],
            AgentResultWithEnvelope(Envelope(success: false, status: "validation_failed", errorCode: CapabilityExecutionFacade.ArgumentValidationFailedCode)),
            Answer("follow_up", rowCount: 0),
            new Dictionary<string, double> { ["validation"] = 2 },
            System.Diagnostics.Stopwatch.GetTimestamp());

        trace.Success.Should().BeTrue();
        trace.ValidationResultJson.Should().Contain("validation_failed");
        trace.ValidationResultJson.Should().Contain("missing");
        trace.RowCount.Should().Be(0);
        trace.AnswerMode.Should().Be(nameof(AnswerMode.Structured));
    }

    [Fact]
    public void FromSuccess_ShouldCaptureSqlExecutionFailureDiagnostics()
    {
        var trace = ToolRoutingTraceFactory.FromSuccess(
            Request(),
            new HardSignalSet(),
            Retrieval(),
            [Tool(Metadata("warehouse.inventory.by-item"))],
            AgentResultWithEnvelope(Envelope(success: false, status: "failed", errorCode: "SQL_RESULT_PARSE_FAILED")),
            Answer("error", rowCount: 0),
            new Dictionary<string, double> { ["sql_execution"] = 9 },
            System.Diagnostics.Stopwatch.GetTimestamp());

        trace.ValidationResultJson.Should().Contain("failed");
        trace.AdapterType.Should().Be("sql");
        trace.StoredProcedure.Should().Be("dbo.ai_warehouse_inventory_by_item");
        trace.LatencyByStageJson.Should().Contain("sql_execution");
    }

    [Fact]
    public void FromFailure_ShouldCaptureAnswerComposerFailureDiagnostics()
    {
        var trace = ToolRoutingTraceFactory.FromFailure(
            Request(),
            new InvalidOperationException("composer failed"),
            System.Diagnostics.Stopwatch.GetTimestamp());

        trace.Success.Should().BeFalse();
        trace.ErrorCode.Should().Be(nameof(InvalidOperationException));
        trace.ModelProvider.Should().Be("microsoft-agent-framework");
        trace.ModelName.Should().Be("gpt-test");
        trace.FallbackUsed.Should().BeFalse();
    }

    private static CapabilitySemanticMetadata Metadata(string key) => new()
    {
        CapabilityKey = key,
        Domain = "warehouse",
        FunctionName = "warehouse_inventory_by_item",
        AdapterType = "sql",
        Operation = "execute_query",
        ExecutionMode = "read"
    };

    private static AgentToolRoutingRequest Request() => new()
    {
        Message = "Show inventory for MADEIRA-BLK",
        Locale = "en-US",
        ExecutionContext = new TilsoftExecutionContext
        {
            TenantId = "tenant-1",
            UserId = "user-1",
            CorrelationId = Guid.NewGuid().ToString(),
            ConversationId = "conv-1"
        },
        RequestedAnswerMode = AnswerMode.Structured,
        Metadata = new Dictionary<string, object?> { ["model"] = "gpt-test" }
    };

    private static CapabilityRetrievalResult Retrieval()
    {
        var metadata = Metadata("warehouse.inventory.by-item");
        return new CapabilityRetrievalResult
        {
            Domains = [new DomainCandidate { Domain = "warehouse", Score = 0.91 }],
            Capabilities = [new CapabilityCandidate { Metadata = metadata, Score = 0.91 }],
            EntityCandidates = [],
            ContextChunks = []
        };
    }

    private static TraceFunction Tool(CapabilitySemanticMetadata metadata) => new(new CapabilityToolDescriptor
    {
        Name = "warehouse_inventory_by_item",
        Description = "Check inventory.",
        ParameterSchema = new JsonObject(),
        Capability = metadata,
        Arguments = []
    });

    private static CapabilityExecutionEnvelope Envelope(bool success, string status, string errorCode) => new()
    {
        CapabilityKey = "warehouse.inventory.by-item",
        ExecutionMode = "read",
        ProcedureName = "dbo.ai_warehouse_inventory_by_item",
        Arguments = new Dictionary<string, object?> { ["@ItemNo"] = "MADEIRA-BLK" },
        RowCount = 0,
        Success = success,
        Status = status,
        MissingArguments = errorCode == CapabilityExecutionFacade.ArgumentValidationFailedCode ? ["@ItemNo"] : [],
        ErrorCode = errorCode,
        ExecutionMetadata = new ExecutionMetadata { AdapterType = "sql", Operation = "execute_query" },
        SensitivityPolicy = SensitivityPolicy.Default,
        AnswerPolicy = AnswerPolicy.Default
    };

    private static AgentRunResult AgentResultWithEnvelope(CapabilityExecutionEnvelope envelope) => new()
    {
        Outcome = AgentRunOutcome.ToolExecution,
        SelectedToolName = "warehouse_inventory_by_item",
        SelectedCapabilityKey = envelope.CapabilityKey,
        Arguments = new JsonObject { ["item_no"] = "MADEIRA-BLK" },
        ToolResult = envelope
    };

    private static AssistantAnswer Answer(string answerType, int rowCount) => new()
    {
        AnswerType = answerType,
        Text = answerType,
        Blocks = [new TextBlock(answerType)],
        Provenance = new AnswerProvenance
        {
            CapabilityKey = "warehouse.inventory.by-item",
            ProcedureName = "dbo.ai_warehouse_inventory_by_item",
            RowCount = rowCount
        }
    };

    private sealed class TraceFunction : AIFunction, ICapabilityBackedAIFunction
    {
        public TraceFunction(CapabilityToolDescriptor descriptor)
        {
            Descriptor = descriptor;
        }

        public CapabilityToolDescriptor Descriptor { get; }

        public OfficialAgentFunctionInvocation? LastInvocation => null;

        public override string Name => Descriptor.Name;

        public override string Description => Descriptor.Description;

        protected override ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<object?>(CapabilityExecutionEnvelope.Succeeded(Descriptor.Capability.CapabilityKey, null));
    }
}
