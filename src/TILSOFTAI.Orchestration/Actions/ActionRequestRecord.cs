namespace TILSOFTAI.Orchestration.Actions;

public static class ActionRequestStatus
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Approved = "Approved";
    public const string Executed = "Executed";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";
    public const string Failed = "Failed";

    public static bool IsActivePending(string? status) =>
        string.Equals(status, Pending, StringComparison.OrdinalIgnoreCase);

    public static bool IsTerminal(string? status) =>
        string.Equals(status, Executed, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Rejected, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Cancelled, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Expired, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Failed, StringComparison.OrdinalIgnoreCase);
}

public sealed class ActionRequestCreateRequest
{
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string ConversationId { get; init; } = string.Empty;
    public string CapabilityKey { get; init; } = string.Empty;
    public string FunctionName { get; init; } = string.Empty;
    public string ProposedToolName { get; init; } = string.Empty;
    public string ProposedProcedureName { get; init; } = string.Empty;
    public string ProposedArgumentsJson { get; init; } = "{}";
    public string? PreviewResultJson { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public string RequestedByUserId { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public string? MetadataJson { get; init; }
}

public sealed class ActionRequestRecord
{
    public string ActionId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string CapabilityKey { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string ProposedProcedureName
    {
        get => ProposedSpName;
        set => ProposedSpName = value;
    }
    public string ProposedArgumentsJson
    {
        get => ArgsJson;
        set => ArgsJson = value;
    }
    public string? PreviewResultJson { get; set; }
    public DateTime CreatedAtUtc
    {
        get => RequestedAtUtc;
        set => RequestedAtUtc = value;
    }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ProposedToolName { get; set; } = string.Empty;
    public string ProposedSpName { get; set; } = string.Empty;
    public string ArgsJson { get; set; } = string.Empty;
    public string RequestedByUserId { get; set; } = string.Empty;
    public DateTime? ConfirmedAtUtc { get; set; }
    public string? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public DateTime? ExecutedAtUtc { get; set; }
    public string? ExecutedByUserId { get; set; }
    public string? CorrelationId { get; set; }
    public string? MetadataJson { get; set; }
    public string? ExecutionResultCompactJson { get; set; }

    public bool IsExpired(DateTime utcNow) =>
        ExpiresAtUtc != default
        && DateTime.SpecifyKind(ExpiresAtUtc, DateTimeKind.Utc) <= utcNow;

    public static ActionRequestRecord FromCreateRequest(ActionRequestCreateRequest request, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestedByUserId = string.IsNullOrWhiteSpace(request.RequestedByUserId)
            ? request.UserId
            : request.RequestedByUserId;

        return new ActionRequestRecord
        {
            TenantId = request.TenantId,
            UserId = string.IsNullOrWhiteSpace(request.UserId) ? requestedByUserId : request.UserId,
            ConversationId = request.ConversationId,
            CapabilityKey = request.CapabilityKey,
            FunctionName = request.FunctionName,
            Status = ActionRequestStatus.Pending,
            ProposedToolName = request.ProposedToolName,
            ProposedSpName = request.ProposedProcedureName,
            ArgsJson = request.ProposedArgumentsJson,
            PreviewResultJson = request.PreviewResultJson,
            RequestedByUserId = requestedByUserId,
            RequestedAtUtc = utcNow,
            ExpiresAtUtc = request.ExpiresAtUtc == default ? utcNow.AddHours(24) : request.ExpiresAtUtc,
            CorrelationId = request.CorrelationId,
            MetadataJson = request.MetadataJson
        };
    }
}
