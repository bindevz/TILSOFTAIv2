namespace TILSOFTAI.Domain.Common;

/// <summary>
/// Represents an error with a code, message, optional exception, and context.
/// </summary>
public sealed record Error(
    string Code,
    string Message,
    Exception? Exception = null,
    IDictionary<string, object>? Context = null)
{
    public static readonly Error Unknown = new("UNKNOWN", "An unknown error occurred.");
    
    public Error WithContext(string key, object value)
    {
        var ctx = Context is null 
            ? new Dictionary<string, object>() 
            : new Dictionary<string, object>(Context);
        ctx[key] = value;
        return this with { Context = ctx };
    }
    
    public Error WithException(Exception ex)
        => this with { Exception = ex };
}

/// <summary>
/// Common error factory methods.
/// </summary>
public static class Errors
{
    // Cache errors
    public static Error CacheReadFailed(string key, Exception? ex = null)
        => new("CACHE_READ_FAILED", $"Failed to read from cache: {key}", ex);
    
    public static Error CacheWriteFailed(string key, Exception? ex = null)
        => new("CACHE_WRITE_FAILED", $"Failed to write to cache: {key}", ex);
    
    // SQL errors
    public static Error SqlExecutionFailed(string sp, Exception? ex = null)
        => new("SQL_EXECUTION_FAILED", $"Stored procedure execution failed: {sp}", ex);
    
    public static Error SqlTimeout(string sp)
        => new("SQL_TIMEOUT", $"SQL command timed out: {sp}");
    
    public static Error SqlConnectionFailed(Exception? ex = null)
        => new("SQL_CONNECTION_FAILED", "Failed to connect to SQL Server", ex);
    
    // Tool errors
    public static Error ToolNotFound(string toolName)
        => new("TOOL_NOT_FOUND", $"Tool not found: {toolName}");
    
    public static Error ToolExecutionFailed(string toolName, Exception? ex = null)
        => new("TOOL_EXECUTION_FAILED", $"Tool execution failed: {toolName}", ex);
    
    // LLM errors
    public static Error LlmRequestFailed(Exception? ex = null)
        => new("LLM_REQUEST_FAILED", "LLM request failed", ex);
    
    public static Error LlmTimeout()
        => new("LLM_TIMEOUT", "LLM request timed out");
    
    // Validation errors
    public static Error ValidationFailed(string field, string reason)
        => new("VALIDATION_FAILED", $"Validation failed for {field}: {reason}");
}
