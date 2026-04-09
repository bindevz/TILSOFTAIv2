using TILSOFTAI.Agents.Abstractions;

namespace TILSOFTAI.Supervisor;

public sealed class SupervisorResult
{
    public bool Success { get; private init; }
    public string? Output { get; private init; }
    public string? Error { get; private init; }
    public string? Code { get; private init; }
    public object? Detail { get; private init; }
    public string? SelectedAgentId { get; private init; }

    public static SupervisorResult Ok(string output, string? selectedAgentId = null) => new()
    {
        Success = true,
        Output = output,
        SelectedAgentId = selectedAgentId
    };

    public static SupervisorResult Fail(
        string error,
        string? code = null,
        object? detail = null,
        string? selectedAgentId = null) => new()
    {
        Success = false,
        Error = error,
        Code = code,
        Detail = detail,
        SelectedAgentId = selectedAgentId
    };

    public static SupervisorResult FromAgentResult(AgentResult result, string? selectedAgentId) =>
        result.Success
            ? Ok(result.Output ?? string.Empty, selectedAgentId)
            : Fail(result.Error ?? "Agent execution failed.", result.Code, result.Detail, selectedAgentId);
}
