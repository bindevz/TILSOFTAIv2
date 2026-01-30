namespace TILSOFTAI.Domain.Errors;

/// <summary>
/// Typed exception for API errors with stable error codes and HTTP status mapping.
/// Controllers should throw this exception instead of creating ErrorEnvelope manually.
/// </summary>
public class TilsoftApiException : Exception
{
    /// <summary>
    /// Stable error code (e.g., CHAT_FAILED, TOOL_ARGS_INVALID).
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// HTTP status code to return (e.g., 400, 401, 500).
    /// </summary>
    public int HttpStatusCode { get; }

    /// <summary>
    /// Optional detail object to include in error response.
    /// </summary>
    public object? Detail { get; }

    public TilsoftApiException(string code, int httpStatusCode, string? message = null, object? detail = null)
        : base(message)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        HttpStatusCode = httpStatusCode;
        Detail = detail;
    }
}
