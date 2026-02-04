using System.Text.Json.Serialization;

namespace TILSOFTAI.Domain.Analytics;

/// <summary>
/// Result of plan validation with structured error contract.
/// </summary>
public sealed class PlanValidationResult
{
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("suggestions")]
    public List<string> Suggestions { get; set; } = new();

    [JsonPropertyName("retryable")]
    public bool Retryable { get; set; } = true;

    public static PlanValidationResult Success() => new() { IsValid = true };

    public static PlanValidationResult Fail(
        string errorCode, 
        string message, 
        bool retryable = true,
        params string[] suggestions) => new()
    {
        IsValid = false,
        ErrorCode = errorCode,
        ErrorMessage = message,
        Retryable = retryable,
        Suggestions = suggestions.ToList()
    };
}

/// <summary>
/// Error codes for plan validation.
/// </summary>
public static class PlanValidationErrorCodes
{
    public const string InvalidJson = "INVALID_JSON";
    public const string MissingDataset = "MISSING_DATASET";
    public const string DatasetNotFound = "DATASET_NOT_FOUND";
    public const string UnknownField = "UNKNOWN_FIELD";
    public const string InvalidOp = "INVALID_OP";
    public const string LimitExceeded = "LIMIT_EXCEEDED";
    public const string GroupByExceeded = "GROUPBY_EXCEEDED";
    public const string MetricsExceeded = "METRICS_EXCEEDED";
    public const string JoinsExceeded = "JOINS_EXCEEDED";
    public const string SecurityViolation = "SECURITY_VIOLATION";
    public const string TimeWindowExceeded = "TIME_WINDOW_EXCEEDED";
}
