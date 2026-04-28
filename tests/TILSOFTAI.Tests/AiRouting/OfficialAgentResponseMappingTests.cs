using FluentAssertions;
using System.Text.Json.Nodes;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.AiRouting;
using TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.Execution;
using TILSOFTAI.Orchestration.Semantic;
using Xunit;

namespace TILSOFTAI.Tests.AiRouting;

public sealed class OfficialAgentResponseMappingTests
{
    [Fact]
    public void OfficialAgentResponseMapper_ShouldDetectClarificationWhenNoToolRuns()
    {
        var result = OfficialAgentResponseMapper.ToAgentRunResult(
            "Which item number should I check?",
            invocation: null);

        result.Outcome.Should().Be(AgentRunOutcome.Clarification);
        result.ToolResult.Should().BeNull();
        result.SelectedCapabilityKey.Should().BeNull();
        result.ClarificationQuestion.Should().Be("Which item number should I check?");
    }

    [Fact]
    public void AgentToolCallResultMapper_ShouldMapClarificationWithoutFallingBackToFirstCandidate()
    {
        var agentResult = new AgentRunResult
        {
            Outcome = AgentRunOutcome.Clarification,
            ClarificationQuestion = "Which item number should I check?"
        };

        var request = AgentRequest(AnswerMode.Structured);
        var mapped = AgentToolCallResultMapper.ToAnswerComposerRequest(
            agentResult,
            request,
            RetrievalWithFirstCandidate("warehouse.inventory.by-item"));

        mapped.CapabilityKey.Should().Be("agent.no-tool");
        mapped.ClarificationQuestion.Should().Be("Which item number should I check?");
        mapped.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task RawJsonMode_ShouldReturnToolEnvelopeAndIgnoreOfficialFinalText()
    {
        var agentResult = ToolResultWithFinalText("This text came from the agent and should not be returned.");
        var mapped = AgentToolCallResultMapper.ToAnswerComposerRequest(
            agentResult,
            AgentRequest(AnswerMode.RawJson),
            RetrievalWithFirstCandidate("warehouse.inventory.by-item"));

        var answer = await CreateComposer().ComposeAsync(mapped, CancellationToken.None);

        answer.AnswerType.Should().Be("raw_json");
        answer.Text.Should().BeEmpty();
        answer.Detail.Should().NotBeNull();
        answer.Detail!.ToString().Should().NotContain("should not be returned");
        answer.Provenance.CapabilityKey.Should().Be("warehouse.inventory.by-item");
        answer.Provenance.RowCount.Should().Be(1);
    }

    [Fact]
    public async Task StructuredMode_ShouldRenderComposerBlocksAndIgnoreOfficialFinalText()
    {
        var agentResult = ToolResultWithFinalText("The agent final text is not the structured answer.");
        var mapped = AgentToolCallResultMapper.ToAnswerComposerRequest(
            agentResult,
            AgentRequest(AnswerMode.Structured),
            RetrievalWithFirstCandidate("warehouse.inventory.by-item"));

        var answer = await CreateComposer().ComposeAsync(mapped, CancellationToken.None);

        answer.AnswerType.Should().Be("structured");
        answer.Text.Should().Contain("Found 1 rows for warehouse.inventory.by-item");
        answer.Text.Should().NotContain("agent final text");
        answer.Blocks.OfType<TableBlock>().Should().ContainSingle();
    }

    private static AgentRunResult ToolResultWithFinalText(string finalText) => new()
    {
        Outcome = AgentRunOutcome.ToolExecution,
        SelectedToolName = "warehouse_inventory_by_item",
        SelectedCapabilityKey = "warehouse.inventory.by-item",
        Arguments = new JsonObject { ["item_no"] = "CHAIR-001" },
        RawText = finalText,
        ToolResult = new CapabilityExecutionEnvelope
        {
            CapabilityKey = "warehouse.inventory.by-item",
            ExecutionMode = "readonly",
            ProcedureName = "dbo.ai_warehouse_inventory_by_item",
            Arguments = new Dictionary<string, object?> { ["@ItemNo"] = "CHAIR-001" },
            Rows =
            [
                new Dictionary<string, object?>
                {
                    ["ItemNo"] = "CHAIR-001",
                    ["AvailableQty"] = 12m
                }
            ],
            RowCount = 1,
            ExecutionMetadata = new ExecutionMetadata
            {
                AdapterType = "sql",
                Operation = "execute_query",
                CorrelationId = "corr-32-5"
            },
            SensitivityPolicy = SensitivityPolicy.Default,
            AnswerPolicy = AnswerPolicy.Default,
            Success = true,
            Status = "succeeded"
        }
    };

    private static AgentToolRoutingRequest AgentRequest(AnswerMode mode) => new()
    {
        Message = "stock for CHAIR-001",
        Locale = "en-US",
        RequestedAnswerMode = mode,
        ExecutionContext = new TilsoftExecutionContext
        {
            TenantId = "tenant-32",
            UserId = "user-32",
            CorrelationId = "corr-32-5"
        }
    };

    private static CapabilityRetrievalResult RetrievalWithFirstCandidate(string capabilityKey) => new()
    {
        Domains = [new DomainCandidate { Domain = "warehouse", Score = 0.9 }],
        Capabilities =
        [
            new CapabilityCandidate
            {
                Score = 0.9,
                Metadata = new CapabilitySemanticMetadata
                {
                    CapabilityKey = capabilityKey,
                    Domain = "warehouse",
                    FunctionName = "warehouse_inventory_by_item",
                    AdapterType = "sql",
                    Operation = "read",
                    ExecutionMode = "read"
                }
            }
        ],
        EntityCandidates = [],
        ContextChunks = []
    };

    private static StructuredAnswerComposer CreateComposer() =>
        new(new RawJsonAnswerComposer(), new AiSummaryService());
}
