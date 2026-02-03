using System.Text.Json;

namespace TILSOFTAI.Domain.Audit;

/// <summary>
/// Audit event for security-related incidents.
/// </summary>
public sealed class SecurityAuditEvent : AuditEvent
{
    /// <summary>
    /// Description of the security incident.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Severity of the incident.
    /// </summary>
    public string Severity { get; init; } = "Medium";

    /// <summary>
    /// Action taken in response (blocked, warned, logged).
    /// </summary>
    public string ActionTaken { get; init; } = "Logged";

    /// <summary>
    /// Creates an input validation failure event.
    /// </summary>
    public static SecurityAuditEvent InputValidationFailure(
        string tenantId,
        string userId,
        string correlationId,
        string ipAddress,
        string userAgent,
        string description,
        object? details = null)
    {
        return new SecurityAuditEvent
        {
            EventType = AuditEventType.Security_InputValidationFailure,
            TenantId = tenantId,
            UserId = userId,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Outcome = AuditOutcome.Denied,
            Description = description,
            Severity = "Low",
            ActionTaken = "Rejected",
            Details = details != null ? JsonDocument.Parse(JsonSerializer.Serialize(details)) : null
        };
    }

    /// <summary>
    /// Creates a rate limit exceeded event.
    /// </summary>
    public static SecurityAuditEvent RateLimitExceeded(
        string tenantId,
        string userId,
        string correlationId,
        string ipAddress,
        string userAgent,
        string endpoint,
        int requestCount,
        int limit)
    {
        return new SecurityAuditEvent
        {
            EventType = AuditEventType.Security_RateLimitExceeded,
            TenantId = tenantId,
            UserId = userId,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Outcome = AuditOutcome.Denied,
            Description = $"Rate limit exceeded for {endpoint}",
            Severity = "Medium",
            ActionTaken = "Rejected",
            Details = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                endpoint,
                requestCount,
                limit
            }))
        };
    }

    /// <summary>
    /// Creates a prompt injection detected event.
    /// </summary>
    public static SecurityAuditEvent PromptInjectionDetected(
        string tenantId,
        string userId,
        string correlationId,
        string ipAddress,
        string userAgent,
        string severity,
        bool wasBlocked)
    {
        return new SecurityAuditEvent
        {
            EventType = AuditEventType.Security_PromptInjectionDetected,
            TenantId = tenantId,
            UserId = userId,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Outcome = wasBlocked ? AuditOutcome.Denied : AuditOutcome.Success,
            Description = "Potential prompt injection detected",
            Severity = severity,
            ActionTaken = wasBlocked ? "Blocked" : "Warned"
        };
    }

    /// <summary>
    /// Creates a suspicious activity event.
    /// </summary>
    public static SecurityAuditEvent SuspiciousActivity(
        string tenantId,
        string userId,
        string correlationId,
        string ipAddress,
        string userAgent,
        string description,
        string severity,
        object? details = null)
    {
        return new SecurityAuditEvent
        {
            EventType = AuditEventType.Security_SuspiciousActivity,
            TenantId = tenantId,
            UserId = userId,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Outcome = AuditOutcome.Success,
            Description = description,
            Severity = severity,
            ActionTaken = "Logged",
            Details = details != null ? JsonDocument.Parse(JsonSerializer.Serialize(details)) : null
        };
    }
}
