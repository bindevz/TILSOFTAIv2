namespace TILSOFTAI.Domain.Audit;

/// <summary>
/// Audit event for authorization-related actions.
/// </summary>
public sealed class AuthzAuditEvent : AuditEvent
{
    /// <summary>
    /// Resource being accessed (endpoint, tool name, etc.).
    /// </summary>
    public string Resource { get; init; } = string.Empty;

    /// <summary>
    /// Action being performed (read, write, execute).
    /// </summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// Roles required for the action.
    /// </summary>
    public string[] RequiredRoles { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Roles the user has.
    /// </summary>
    public string[] UserRoles { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Name of the authorization policy evaluated.
    /// </summary>
    public string? PolicyName { get; init; }

    /// <summary>
    /// Creates a granted authorization event.
    /// </summary>
    public static AuthzAuditEvent Granted(
        string tenantId,
        string userId,
        string correlationId,
        string ipAddress,
        string userAgent,
        string resource,
        string action,
        string[] userRoles,
        string[] requiredRoles,
        string? policyName = null)
    {
        return new AuthzAuditEvent
        {
            EventType = AuditEventType.Authorization_Granted,
            TenantId = tenantId,
            UserId = userId,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Outcome = AuditOutcome.Success,
            Resource = resource,
            Action = action,
            UserRoles = userRoles,
            RequiredRoles = requiredRoles,
            PolicyName = policyName
        };
    }

    /// <summary>
    /// Creates a denied authorization event.
    /// </summary>
    public static AuthzAuditEvent Denied(
        string tenantId,
        string userId,
        string correlationId,
        string ipAddress,
        string userAgent,
        string resource,
        string action,
        string[] userRoles,
        string[] requiredRoles,
        string? policyName = null)
    {
        return new AuthzAuditEvent
        {
            EventType = AuditEventType.Authorization_Denied,
            TenantId = tenantId,
            UserId = userId,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Outcome = AuditOutcome.Denied,
            Resource = resource,
            Action = action,
            UserRoles = userRoles,
            RequiredRoles = requiredRoles,
            PolicyName = policyName
        };
    }
}
