namespace TILSOFTAI.Approvals;

public interface IApprovalEngine
{
    Task<ProposedActionRecord> CreateAsync(ProposedAction action, ApprovalContext context, CancellationToken ct);
    Task<ProposedActionRecord> ApproveAsync(string actionId, ApprovalContext context, CancellationToken ct);
    Task<ProposedActionRecord> RejectAsync(string actionId, ApprovalContext context, CancellationToken ct);
    Task<ActionExecutionResult> ExecuteAsync(string actionId, ApprovalContext context, CancellationToken ct);
}
