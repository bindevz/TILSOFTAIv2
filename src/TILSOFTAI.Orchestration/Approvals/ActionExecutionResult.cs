namespace TILSOFTAI.Approvals;

public sealed class ActionExecutionResult
{
    public ProposedActionRecord Action { get; set; } = new();
    public string RawResult { get; set; } = string.Empty;
    public string CompactedResult { get; set; } = string.Empty;
}
