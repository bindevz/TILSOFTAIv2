using TILSOFTAI.Orchestration.Execution;

namespace TILSOFTAI.Orchestration.Answering;

public sealed record AnswerComposerRequest
{
    public required AnswerMode Mode { get; init; }
    public required string CapabilityKey { get; init; }
    public required string? ProcedureName { get; init; }
    public required IReadOnlyDictionary<string, object?> Arguments { get; init; }
    public required ResultSchema? ResultSchema { get; init; }
    public object? Result { get; init; }
    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; }
    public required int RowCount { get; init; }
    public required ExecutionMetadata ExecutionMetadata { get; init; }
    public required SensitivityPolicy SensitivityPolicy { get; init; }
    public required string Locale { get; init; }
    public required AnswerPolicy AnswerPolicy { get; init; }
    public string? ClarificationQuestion { get; init; }
    public IReadOnlyList<string> MissingArguments { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> InvalidArguments { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, object?>? DraftAction { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

public enum AnswerMode
{
    Structured,
    RawJson
}
