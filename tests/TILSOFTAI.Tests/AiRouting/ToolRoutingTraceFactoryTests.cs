using System.Text.Json.Nodes;
using FluentAssertions;
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
                CorrelationId = Guid.NewGuid().ToString()
            },
            RequestedAnswerMode = AnswerMode.Structured
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
        var tool = new AgentFunctionTool
        {
            Name = "warehouse_inventory_by_item",
            Description = "Check inventory.",
            ParameterSchema = new JsonObject(),
            Capability = metadata,
            Arguments = [],
            InvokeAsync = (_, _) => Task.FromResult(CapabilityExecutionEnvelope.Succeeded(metadata.CapabilityKey, null))
        };
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
            AnswerPolicy = AnswerPolicy.Default
        };
        var agentResult = new AgentRunResult
        {
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
        trace.ArgumentsBeforeNormalizationJson.Should().Contain("item_no");
        trace.ArgumentsAfterNormalizationJson.Should().Contain("@ItemNo");
        trace.AdapterType.Should().Be("sql");
        trace.RowCount.Should().Be(1);
        trace.LatencyByStageJson.Should().Contain("semantic_retrieval");
        trace.ModelProvider.Should().Be("microsoft-agent-framework");
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
}
