namespace TILSOFTAI.Orchestration.Actions;

public sealed class ActionExecutionResult
{
    public ActionRequestRecord ActionRequest { get; set; } = new();
    public string RawResult { get; set; } = string.Empty;
    public string CompactedResult { get; set; } = string.Empty;
}
