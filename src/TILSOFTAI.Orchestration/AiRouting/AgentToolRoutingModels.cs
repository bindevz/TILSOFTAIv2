using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Answering;

namespace TILSOFTAI.Orchestration.AiRouting;

public sealed record AgentToolRoutingRequest
{
    public required string Message { get; init; }
    public required TilsoftExecutionContext ExecutionContext { get; init; }
    public required string Locale { get; init; }
    public required AnswerMode RequestedAnswerMode { get; init; }
    public IReadOnlyDictionary<string, object?> Metadata { get; init; } =
        new Dictionary<string, object?>();
}

public sealed record AgentToolRoutingResult
{
    public required bool Handled { get; init; }
    public AssistantAnswer? Answer { get; init; }
    public string? FailureReason { get; init; }
}
