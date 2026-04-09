namespace TILSOFTAI.Tools.Abstractions;

public interface IToolAdapter
{
    string AdapterType { get; }

    Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken ct);
}
