namespace TILSOFTAI.Domain.Errors;

public static class ErrorCode
{
    public const string InvalidArgument = "INVALID_ARGUMENT";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string ToolValidationFailed = "TOOL_VALIDATION_FAILED";
    public const string ToolExecutionFailed = "TOOL_EXECUTION_FAILED";
    public const string LlmTransportError = "LLM_TRANSPORT_ERROR";
    public const string SqlError = "SQL_ERROR";
    public const string ChatFailed = "CHAT_FAILED";
    public const string UnhandledError = "UNHANDLED_ERROR";
}
