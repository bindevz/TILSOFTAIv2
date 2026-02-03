namespace TILSOFTAI.Domain.Errors;

public static class ErrorCode
{
    public const string InvalidArgument = "INVALID_ARGUMENT";
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    
    // Tool-related errors
    public const string ToolValidationFailed = "TOOL_VALIDATION_FAILED";
    public const string ToolExecutionFailed = "TOOL_EXECUTION_FAILED";
    public const string ToolArgsInvalid = "TOOL_ARGS_INVALID";
    
    // Auth-related errors
    public const string TenantMismatch = "TENANT_MISMATCH";
    
    // Write action errors
    public const string WriteActionArgsInvalid = "WRITE_ACTION_ARGS_INVALID";
    public const string WriteActionDisabled = "WRITE_ACTION_DISABLED";
    public const string WriteActionNotFound = "WRITE_ACTION_NOT_FOUND";
    
    // Infrastructure errors
    public const string LlmTransportError = "LLM_TRANSPORT_ERROR";
    public const string SqlError = "SQL_ERROR";
    public const string ChatFailed = "CHAT_FAILED";
    public const string UnhandledError = "UNHANDLED_ERROR";
    public const string RequestTooLarge = "REQUEST_TOO_LARGE";

    // Input validation errors
    public const string InvalidInput = "INVALID_INPUT";
    public const string InputTooLong = "INPUT_TOO_LONG";
    public const string ForbiddenPattern = "FORBIDDEN_PATTERN";
    public const string PromptInjectionDetected = "PROMPT_INJECTION_DETECTED";
    /// <summary>
    /// The external service is unavailable (e.g. circuit breaker open).
    /// </summary>
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";

    /// <summary>
    /// An external dependency failed.
    /// </summary>
    public const string DependencyFailure = "DEPENDENCY_FAILURE";

    /// <summary>
    /// The circuit breaker is open.
    /// </summary>
    public const string CircuitOpen = "CIRCUIT_OPEN";
}
