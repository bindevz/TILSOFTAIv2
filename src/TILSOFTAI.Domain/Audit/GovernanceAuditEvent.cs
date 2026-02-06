namespace TILSOFTAI.Domain.Audit;

/// <summary>
/// PATCH 31.06: Audit event for tool governance decisions.
/// Covers both allow and deny outcomes across all execution paths.
/// </summary>
public sealed record GovernanceAuditEvent
{
    public required string EventType { get; init; }       // "governance.allow" | "governance.deny"
    public required string TenantId { get; init; }
    public required string UserId { get; init; }
    public required string CorrelationId { get; init; }
    public required string ToolName { get; init; }
    public required string ExecutionSource { get; init; }  // "llm_tool_call" | "orchestrator" | "api"
    public required string[] UserRoles { get; init; }
    public required string[] RequiredRoles { get; init; }
    public string? DenialReason { get; init; }
    public string? DenialCode { get; init; }
    public bool SchemaValid { get; init; }
    public bool InputSanitized { get; init; }
    public double DurationMs { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public static GovernanceAuditEvent Allowed(
        string tenantId, string userId, string correlationId,
        string toolName, string source, string[] userRoles, string[] requiredRoles,
        double durationMs) => new()
    {
        EventType = "governance.allow",
        TenantId = tenantId,
        UserId = userId,
        CorrelationId = correlationId,
        ToolName = toolName,
        ExecutionSource = source,
        UserRoles = userRoles,
        RequiredRoles = requiredRoles,
        SchemaValid = true,
        InputSanitized = true,
        DurationMs = durationMs
    };

    public static GovernanceAuditEvent Denied(
        string tenantId, string userId, string correlationId,
        string toolName, string source, string[] userRoles, string[] requiredRoles,
        string reason, string? code, double durationMs) => new()
    {
        EventType = "governance.deny",
        TenantId = tenantId,
        UserId = userId,
        CorrelationId = correlationId,
        ToolName = toolName,
        ExecutionSource = source,
        UserRoles = userRoles,
        RequiredRoles = requiredRoles,
        DenialReason = reason,
        DenialCode = code,
        DurationMs = durationMs
    };
}
