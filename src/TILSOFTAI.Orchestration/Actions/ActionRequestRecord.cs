namespace TILSOFTAI.Orchestration.Actions;

public sealed class ActionRequestRecord
{
    public string ActionId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ProposedToolName { get; set; } = string.Empty;
    public string ProposedSpName { get; set; } = string.Empty;
    public string ArgsJson { get; set; } = string.Empty;
    public string RequestedByUserId { get; set; } = string.Empty;
    public string? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? ExecutedAtUtc { get; set; }
    public string? ExecutionResultCompactJson { get; set; }
}
