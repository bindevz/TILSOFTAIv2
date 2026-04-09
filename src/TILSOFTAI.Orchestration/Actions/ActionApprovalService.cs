using TILSOFTAI.Approvals;
using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Actions;

[Obsolete("Use TILSOFTAI.Approvals.IApprovalEngine. This service remains only as a Sprint 1 compatibility facade.")]
public sealed class ActionApprovalService
{
    private readonly IApprovalEngine _approvalEngine;

    public ActionApprovalService(IApprovalEngine approvalEngine)
    {
        _approvalEngine = approvalEngine ?? throw new ArgumentNullException(nameof(approvalEngine));
    }

    public async Task<ActionRequestRecord> CreateAsync(
        TilsoftExecutionContext context,
        string toolName,
        string proposedSpName,
        string argsJson,
        CancellationToken ct)
    {
        var action = new ProposedAction
        {
            ActionType = "write",
            AgentId = "legacy-approval-facade",
            TargetSystem = "sql",
            CapabilityKey = toolName,
            PayloadJson = argsJson,
            ApprovalRequirement = "required",
            ToolName = toolName,
            StoredProcedure = proposedSpName
        };

        var record = await _approvalEngine.CreateAsync(action, ApprovalContext.FromExecutionContext(context), ct);
        return MapRecord(record);
    }

    public async Task<ActionRequestRecord> ApproveAsync(TilsoftExecutionContext context, string actionId, CancellationToken ct)
    {
        var record = await _approvalEngine.ApproveAsync(actionId, ApprovalContext.FromExecutionContext(context), ct);
        return MapRecord(record);
    }

    public async Task<ActionRequestRecord> RejectAsync(TilsoftExecutionContext context, string actionId, CancellationToken ct)
    {
        var record = await _approvalEngine.RejectAsync(actionId, ApprovalContext.FromExecutionContext(context), ct);
        return MapRecord(record);
    }

    public async Task<ActionExecutionResult> ExecuteAsync(TilsoftExecutionContext context, string actionId, CancellationToken ct)
    {
        var result = await _approvalEngine.ExecuteAsync(actionId, ApprovalContext.FromExecutionContext(context), ct);
        return new ActionExecutionResult
        {
            ActionRequest = MapRecord(result.Action),
            RawResult = result.RawResult,
            CompactedResult = result.CompactedResult
        };
    }

    private static ActionRequestRecord MapRecord(ProposedActionRecord record) => new()
    {
        ActionId = record.ActionId,
        TenantId = record.TenantId,
        ConversationId = record.ConversationId,
        RequestedAtUtc = record.RequestedAtUtc,
        Status = record.Status,
        ProposedToolName = record.ToolName ?? record.CapabilityKey,
        ProposedSpName = record.StoredProcedure ?? string.Empty,
        ArgsJson = record.PayloadJson,
        RequestedByUserId = record.RequestedByUserId,
        ApprovedByUserId = record.ApprovedByUserId,
        ApprovedAtUtc = record.ApprovedAtUtc,
        ExecutedAtUtc = record.ExecutedAtUtc,
        ExecutionResultCompactJson = record.ExecutionResultCompactJson
    };
}
