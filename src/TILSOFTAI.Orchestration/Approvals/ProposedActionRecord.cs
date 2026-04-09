namespace TILSOFTAI.Approvals;

public sealed class ProposedActionRecord
{
    public string ActionId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string TargetSystem { get; set; } = string.Empty;
    public string CapabilityKey { get; set; } = string.Empty;
    public string? ToolName { get; set; }
    public string? StoredProcedure { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string? DiffPreviewJson { get; set; }
    public string? RiskLevel { get; set; }
    public string? ApprovalRequirement { get; set; }
    public string RequestedByUserId { get; set; } = string.Empty;
    public string? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? ExecutedAtUtc { get; set; }
    public string? ExecutionResultCompactJson { get; set; }
}
