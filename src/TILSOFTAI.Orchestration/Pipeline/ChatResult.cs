namespace TILSOFTAI.Orchestration.Pipeline;

public sealed class ChatResult
{
    public bool Success { get; private init; }
    public string? Content { get; private init; }
    public string? Error { get; private init; }

    public static ChatResult Ok(string content) => new()
    {
        Success = true,
        Content = content
    };

    public static ChatResult Fail(string error) => new()
    {
        Success = false,
        Error = error
    };
}
