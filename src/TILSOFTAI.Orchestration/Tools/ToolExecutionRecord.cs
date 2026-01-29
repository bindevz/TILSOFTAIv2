namespace TILSOFTAI.Orchestration.Tools;

public sealed class ToolExecutionRecord
{
    public string ToolName { get; set; } = string.Empty;
    public string? SpName { get; set; }
    public string ArgumentsJson { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string CompactedResult { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public long DurationMs { get; set; }
}
