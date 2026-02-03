namespace TILSOFTAI.Domain.Validation;

/// <summary>
/// Represents a validation error with localization support.
/// </summary>
public sealed class ValidationError
{
    /// <summary>
    /// Error code for programmatic handling (e.g., INVALID_INPUT, INPUT_TOO_LONG).
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Field name that caused the error.
    /// </summary>
    public string? Field { get; init; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Message key for localization lookup.
    /// </summary>
    public string? MessageKey { get; init; }

    /// <summary>
    /// Additional data for the error (e.g., max length, actual length).
    /// </summary>
    public IDictionary<string, object>? Data { get; init; }
}

/// <summary>
/// Result of input validation containing validation status, errors, and sanitized output.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Whether the input passed validation.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// List of validation errors if any.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; init; } = Array.Empty<ValidationError>();

    /// <summary>
    /// The sanitized input value (after normalization, escaping, etc.).
    /// </summary>
    public string? SanitizedValue { get; init; }

    /// <summary>
    /// The original input value before sanitization (for audit purposes).
    /// </summary>
    public string? OriginalValue { get; init; }

    /// <summary>
    /// Prompt injection detection severity level (None, Low, Medium, High).
    /// </summary>
    public PromptInjectionSeverity InjectionSeverity { get; init; } = PromptInjectionSeverity.None;

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success(string originalValue, string sanitizedValue) => new()
    {
        IsValid = true,
        OriginalValue = originalValue,
        SanitizedValue = sanitizedValue,
        Errors = Array.Empty<ValidationError>()
    };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static ValidationResult Failure(string originalValue, params ValidationError[] errors) => new()
    {
        IsValid = false,
        OriginalValue = originalValue,
        SanitizedValue = null,
        Errors = errors
    };

    /// <summary>
    /// Creates a result with prompt injection warning but still valid.
    /// </summary>
    public static ValidationResult SuccessWithWarning(
        string originalValue,
        string sanitizedValue,
        PromptInjectionSeverity severity) => new()
    {
        IsValid = true,
        OriginalValue = originalValue,
        SanitizedValue = sanitizedValue,
        InjectionSeverity = severity,
        Errors = Array.Empty<ValidationError>()
    };
}

/// <summary>
/// Severity level for prompt injection detection.
/// </summary>
public enum PromptInjectionSeverity
{
    /// <summary>
    /// No injection detected.
    /// </summary>
    None = 0,

    /// <summary>
    /// Low severity - possible false positive.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Medium severity - suspicious patterns detected.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// High severity - clear injection attempt detected.
    /// </summary>
    High = 3
}
