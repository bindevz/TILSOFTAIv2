namespace TILSOFTAI.Approvals;

public sealed class ProposedAction
{
    public string ActionType { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string TargetSystem { get; set; } = string.Empty;
    public string CapabilityKey { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string? DiffPreviewJson { get; set; }
    public string? RiskLevel { get; set; }
    public string? ApprovalRequirement { get; set; }
    public string? ToolName { get; set; }
    public string? StoredProcedure { get; set; }
}
