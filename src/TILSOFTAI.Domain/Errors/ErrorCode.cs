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
}
