using TILSOFTAI.Domain.Errors;

namespace TILSOFTAI.Orchestration.Pipeline;

public sealed class ChatResult
{
    public bool Success { get; private init; }
    public string? Content { get; private init; }
    public string? Error { get; private init; }
    public string? Code { get; private init; }
    public object? Detail { get; private init; }

    public static ChatResult Ok(string content) => new()
    {
        Success = true,
        Content = content
    };

    public static ChatResult Fail(string error, string? code = null, object? detail = null) => new()
    {
        Success = false,
        Error = error,
        Code = string.IsNullOrWhiteSpace(code) ? ErrorCode.ChatFailed : code,
        Detail = detail
    };
}
