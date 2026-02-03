namespace TILSOFTAI.Domain.Audit;

/// <summary>
/// Audit event for authentication-related actions.
/// </summary>
public sealed class AuthAuditEvent : AuditEvent
{
    /// <summary>
    /// Authentication method used (JWT, ApiKey, etc.).
    /// </summary>
    public string AuthMethod { get; init; } = "JWT";

    /// <summary>
    /// Reason for authentication failure, if applicable.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Token claims (sensitive values redacted).
    /// </summary>
    public Dictionary<string, string> TokenClaims { get; init; } = new();

    /// <summary>
    /// Creates a success event for authentication.
    /// </summary>
    public static AuthAuditEvent Success(
        string tenantId,
        string userId,
        string correlationId,
        string ipAddress,
        string userAgent,
        Dictionary<string, string>? claims = null)
    {
        return new AuthAuditEvent
        {
            EventType = AuditEventType.Authentication_Success,
            TenantId = tenantId,
            UserId = userId,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Outcome = AuditOutcome.Success,
            TokenClaims = claims ?? new()
        };
    }

    /// <summary>
    /// Creates a failure event for authentication.
    /// </summary>
    public static AuthAuditEvent Failure(
        string correlationId,
        string ipAddress,
        string userAgent,
        string failureReason)
    {
        return new AuthAuditEvent
        {
            EventType = AuditEventType.Authentication_Failure,
            TenantId = string.Empty,
            UserId = string.Empty,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Outcome = AuditOutcome.Failure,
            FailureReason = failureReason
        };
    }
}
