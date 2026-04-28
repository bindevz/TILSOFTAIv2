using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.Execution;

namespace TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;

public static class AgentToolCallResultMapper
{
    public static AnswerComposerRequest ToAnswerComposerRequest(
        AgentRunResult agentResult,
        AgentToolRoutingRequest request,
        TILSOFTAI.Orchestration.Semantic.CapabilityRetrievalResult retrieval)
    {
        var envelope = agentResult.ToolResult;
        return new AnswerComposerRequest
        {
            Mode = request.RequestedAnswerMode,
            CapabilityKey = envelope?.CapabilityKey
                ?? agentResult.SelectedCapabilityKey
                ?? "agent.no-tool",
            ProcedureName = envelope?.ProcedureName,
            Arguments = envelope?.Arguments
                ?? agentResult.Arguments.ToDictionary(
                    pair => pair.Key,
                    pair => (object?)pair.Value?.ToJsonString(),
                    StringComparer.OrdinalIgnoreCase),
            ResultSchema = envelope?.ResultSchema,
            Result = envelope?.Result,
            Rows = envelope?.Rows ?? Array.Empty<IReadOnlyDictionary<string, object?>>(),
            RowCount = envelope?.RowCount ?? 0,
            ExecutionMetadata = envelope?.ExecutionMetadata ?? ExecutionMetadata.Empty,
            SensitivityPolicy = envelope?.SensitivityPolicy ?? SensitivityPolicy.Default,
            Locale = string.IsNullOrWhiteSpace(request.Locale) ? "vi-VN" : request.Locale,
            AnswerPolicy = envelope?.AnswerPolicy ?? AnswerPolicy.Default,
            ClarificationQuestion = agentResult.ClarificationQuestion ?? envelope?.ClarificationQuestion,
            MissingArguments = envelope?.MissingArguments ?? Array.Empty<string>(),
            InvalidArguments = envelope?.InvalidArguments ?? Array.Empty<string>(),
            DraftAction = envelope?.DraftAction,
            ErrorCode = envelope?.ErrorCode,
            ErrorMessage = envelope?.ErrorMessage
        };
    }
}
