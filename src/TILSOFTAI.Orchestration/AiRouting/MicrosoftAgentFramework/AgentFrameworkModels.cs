using System.Text.Json.Nodes;
using TILSOFTAI.Orchestration.Execution;

namespace TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;

public sealed record AgentRunResult
{
    public AgentRunOutcome Outcome { get; init; } = AgentRunOutcome.NoTool;
    public string? SelectedToolName { get; init; }
    public string? SelectedCapabilityKey { get; init; }
    public JsonObject Arguments { get; init; } = new();
    public CapabilityExecutionEnvelope? ToolResult { get; init; }
    public string? ClarificationQuestion { get; init; }
    public string? RawText { get; init; }
}

public enum AgentRunOutcome
{
    ToolExecution,
    Clarification,
    NoTool
}
