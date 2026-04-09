namespace TILSOFTAI.Approvals;

/// <summary>
/// Runtime guard that verifies a write action has been approved before execution.
/// Called by SqlToolAdapter (and any future adapter) before executing write operations.
/// </summary>
public interface IWriteActionGuard
{
    /// <summary>
    /// Validates that the specified action has been approved and is eligible for execution.
    /// </summary>
    /// <param name="tenantId">Tenant owning the action.</param>
    /// <param name="actionId">The action ID to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Guard result indicating approval status.</returns>
    Task<WriteActionGuardResult> ValidateAsync(string tenantId, string actionId, CancellationToken ct);
}
