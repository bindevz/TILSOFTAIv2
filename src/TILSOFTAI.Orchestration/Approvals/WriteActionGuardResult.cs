namespace TILSOFTAI.Approvals;

/// <summary>
/// Result of write-action guard validation.
/// Indicates whether a write action has been properly approved before execution.
/// </summary>
public sealed class WriteActionGuardResult
{
    public bool IsApproved { get; init; }
    public string? Reason { get; init; }
    public string? ApprovedByUserId { get; init; }

    public static WriteActionGuardResult Approved(string approvedByUserId) => new()
    {
        IsApproved = true,
        ApprovedByUserId = approvedByUserId
    };

    public static WriteActionGuardResult Rejected(string reason) => new()
    {
        IsApproved = false,
        Reason = reason
    };
}
