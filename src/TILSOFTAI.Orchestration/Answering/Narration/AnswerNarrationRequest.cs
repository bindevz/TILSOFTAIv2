using TILSOFTAI.Orchestration.Execution;

namespace TILSOFTAI.Orchestration.Answering.Narration;

public sealed record AnswerNarrationRequest
{
    public required string Locale { get; init; }
    public required string CapabilityKey { get; init; }
    public string? UserQuestion { get; init; }
    public string? CapabilityName { get; init; }
    public string? CapabilityDescription { get; init; }
    public required IReadOnlyDictionary<string, object?> Arguments { get; init; }
    public required ResultSchema? ResultSchema { get; init; }
    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; }
    public required int RowCount { get; init; }
    public required AnswerPolicy AnswerPolicy { get; init; }
    public required SensitivityPolicy SensitivityPolicy { get; init; }
    public required ExecutionMetadata ExecutionMetadata { get; init; }
}
