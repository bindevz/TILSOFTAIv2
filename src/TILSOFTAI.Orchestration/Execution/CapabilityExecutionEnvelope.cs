using TILSOFTAI.Orchestration.Answering;

namespace TILSOFTAI.Orchestration.Execution;

public sealed record CapabilityExecutionEnvelope
{
    public required string CapabilityKey { get; init; }
    public required string ExecutionMode { get; init; }
    public string? ProcedureName { get; init; }
    public required IReadOnlyDictionary<string, object?> Arguments { get; init; }
    public ResultSchema? ResultSchema { get; init; }
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; } = Array.Empty<IReadOnlyDictionary<string, object?>>();
    public int RowCount { get; init; }
    public required ExecutionMetadata ExecutionMetadata { get; init; }
    public required SensitivityPolicy SensitivityPolicy { get; init; }
    public required AnswerPolicy AnswerPolicy { get; init; }
    public string? ClarificationQuestion { get; init; }
    public IReadOnlyList<string> MissingArguments { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> InvalidArguments { get; init; } = Array.Empty<string>();
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public bool Success { get; init; }
    public IReadOnlyDictionary<string, object?>? DraftAction { get; init; }

    public string? Status { get; init; }
    public object? Result { get; init; }
    public string? Error => ErrorMessage;

    public static CapabilityExecutionEnvelope Blocked(string capabilityKey, string reason) => new()
    {
        CapabilityKey = capabilityKey,
        ExecutionMode = "blocked",
        Arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
        ExecutionMetadata = ExecutionMetadata.Empty,
        SensitivityPolicy = SensitivityPolicy.Default,
        AnswerPolicy = AnswerPolicy.Default,
        Success = false,
        Status = "blocked",
        ErrorCode = "CAPABILITY_EXECUTION_BLOCKED",
        ErrorMessage = reason
    };

    public static CapabilityExecutionEnvelope Succeeded(string capabilityKey, object? result) => new()
    {
        CapabilityKey = capabilityKey,
        ExecutionMode = "readonly",
        Arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
        ExecutionMetadata = ExecutionMetadata.Empty,
        SensitivityPolicy = SensitivityPolicy.Default,
        AnswerPolicy = AnswerPolicy.Default,
        Success = true,
        Status = "succeeded",
        Result = result
    };
}

public sealed record ExecutionMetadata
{
    public string? AdapterType { get; init; }
    public string? Operation { get; init; }
    public string? TenantId { get; init; }
    public string? UserId { get; init; }
    public string? CorrelationId { get; init; }
    public string? PayloadJson { get; init; }
    public object? Detail { get; init; }

    public static ExecutionMetadata Empty { get; } = new();
}
