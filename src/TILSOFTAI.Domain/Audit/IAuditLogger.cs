namespace TILSOFTAI.Domain.Audit;

/// <summary>
/// Interface for centralized audit logging of security-relevant events.
/// All methods are fire-and-forget (async internally, but non-blocking for callers).
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Logs an authentication event (login success/failure, logout).
    /// </summary>
    void LogAuthenticationEvent(AuthAuditEvent auditEvent);

    /// <summary>
    /// Logs an authorization event (access granted/denied).
    /// </summary>
    void LogAuthorizationEvent(AuthzAuditEvent auditEvent);

    /// <summary>
    /// Logs a data access event (read/write/delete operations).
    /// </summary>
    void LogDataAccessEvent(DataAccessAuditEvent auditEvent);

    /// <summary>
    /// Logs a security event (validation failures, rate limits, suspicious activity).
    /// </summary>
    void LogSecurityEvent(SecurityAuditEvent auditEvent);

    /// <summary>
    /// Logs a generic audit event.
    /// </summary>
    void Log(AuditEvent auditEvent);
}
