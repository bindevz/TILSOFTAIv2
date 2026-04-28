using TILSOFTAI.Orchestration.AiRouting.Tools;

namespace TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;

public static class OfficialAgentResponseMapper
{
    public static AgentRunResult ToAgentRunResult(
        string? responseText,
        OfficialAgentToolInvocation? invocation)
    {
        if (invocation?.ToolResult is not null)
        {
            return new AgentRunResult
            {
                Outcome = AgentRunOutcome.ToolExecution,
                SelectedToolName = invocation.SourceTool.Name,
                SelectedCapabilityKey = invocation.SourceTool.Capability.CapabilityKey,
                Arguments = invocation.ModelFacingArguments,
                ToolResult = invocation.ToolResult,
                RawText = responseText
            };
        }

        return new AgentRunResult
        {
            Outcome = string.IsNullOrWhiteSpace(responseText)
                ? AgentRunOutcome.NoTool
                : AgentRunOutcome.Clarification,
            ClarificationQuestion = string.IsNullOrWhiteSpace(responseText)
                ? null
                : responseText.Trim(),
            RawText = responseText
        };
    }
}
