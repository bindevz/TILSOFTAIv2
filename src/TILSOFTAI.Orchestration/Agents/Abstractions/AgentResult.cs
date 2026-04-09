namespace TILSOFTAI.Agents.Abstractions;

public sealed class AgentResult
{
    public bool Success { get; private init; }
    public string? Output { get; private init; }
    public string? Error { get; private init; }
    public string? Code { get; private init; }
    public object? Detail { get; private init; }

    public static AgentResult Ok(string output) => new()
    {
        Success = true,
        Output = output
    };

    public static AgentResult Fail(string error, string? code = null, object? detail = null) => new()
    {
        Success = false,
        Error = error,
        Code = code,
        Detail = detail
    };
}
