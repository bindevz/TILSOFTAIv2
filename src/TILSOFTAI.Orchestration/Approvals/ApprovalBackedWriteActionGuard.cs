using Microsoft.Extensions.Logging;
using TILSOFTAI.Orchestration.Actions;

namespace TILSOFTAI.Approvals;

/// <summary>
/// Write-action guard backed by IActionRequestStore.
/// Verifies that the action exists, belongs to the tenant, and has been approved but not yet executed.
/// </summary>
public sealed class ApprovalBackedWriteActionGuard : IWriteActionGuard
{
    private readonly IActionRequestStore _requestStore;
    private readonly ILogger<ApprovalBackedWriteActionGuard> _logger;

    public ApprovalBackedWriteActionGuard(
        IActionRequestStore requestStore,
        ILogger<ApprovalBackedWriteActionGuard> logger)
    {
        _requestStore = requestStore ?? throw new ArgumentNullException(nameof(requestStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WriteActionGuardResult> ValidateAsync(string tenantId, string actionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return WriteActionGuardResult.Rejected("TenantId is required for write-action validation.");
        }

        if (string.IsNullOrWhiteSpace(actionId))
        {
            return WriteActionGuardResult.Rejected("ActionId is required for write-action validation.");
        }

        var record = await _requestStore.GetAsync(tenantId, actionId, ct);
        if (record is null)
        {
            _logger.LogWarning(
                "WriteActionGuard | Rejected | ActionId: {ActionId} | Reason: action_not_found",
                actionId);
            return WriteActionGuardResult.Rejected($"Action '{actionId}' not found for tenant.");
        }

        if (record.IsExpired(DateTime.UtcNow))
        {
            _logger.LogWarning(
                "WriteActionGuard | Rejected | ActionId: {ActionId} | Reason: expired",
                actionId);
            return WriteActionGuardResult.Rejected($"Action '{actionId}' has expired and must be previewed again.");
        }

        if (ActionRequestStatus.IsTerminal(record.Status))
        {
            _logger.LogWarning(
                "WriteActionGuard | Rejected | ActionId: {ActionId} | Status: {Status} | Reason: terminal",
                actionId, record.Status);
            return WriteActionGuardResult.Rejected(
                $"Action '{actionId}' has terminal status '{record.Status}' and cannot execute again.");
        }

        if (!string.Equals(record.Status, ActionRequestStatus.Approved, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "WriteActionGuard | Rejected | ActionId: {ActionId} | Status: {Status} | Reason: not_approved",
                actionId, record.Status);
            return WriteActionGuardResult.Rejected(
                $"Action '{actionId}' has status '{record.Status}'; must be 'Approved' to execute.");
        }

        if (record.ExecutedAtUtc.HasValue)
        {
            _logger.LogWarning(
                "WriteActionGuard | Rejected | ActionId: {ActionId} | Reason: already_executed",
                actionId);
            return WriteActionGuardResult.Rejected($"Action '{actionId}' has already been executed.");
        }

        _logger.LogInformation(
            "WriteActionGuard | Approved | ActionId: {ActionId} | ApprovedBy: {ApprovedBy}",
            actionId, record.ApprovedByUserId ?? "unknown");

        return WriteActionGuardResult.Approved(record.ApprovedByUserId ?? string.Empty);
    }
}
