namespace TILSOFTAI.Tools.Abstractions;

public sealed class ToolExecutionResult
{
    public bool Success { get; private init; }
    public string? PayloadJson { get; private init; }
    public object? Payload { get; private init; }
    public string? ErrorCode { get; private init; }
    public object? Detail { get; private init; }

    public static ToolExecutionResult Ok(string? payloadJson, object? payload = null) => new()
    {
        Success = true,
        PayloadJson = payloadJson,
        Payload = payload
    };

    public static ToolExecutionResult Fail(string errorCode, object? detail = null) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        Detail = detail
    };
}
