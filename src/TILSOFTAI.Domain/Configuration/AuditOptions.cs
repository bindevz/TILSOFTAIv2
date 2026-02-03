using TILSOFTAI.Domain.Audit;

namespace TILSOFTAI.Domain.Configuration;

/// <summary>
/// Configuration options for audit logging.
/// </summary>
public sealed class AuditOptions
{
    /// <summary>
    /// Whether audit logging is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Event types to audit. Default: all types.
    /// </summary>
    public AuditEventType[] EnabledEventTypes { get; set; } = Array.Empty<AuditEventType>();

    /// <summary>
    /// Whether SQL sink is enabled. Default: true.
    /// </summary>
    public bool SqlEnabled { get; set; } = true;

    /// <summary>
    /// Whether file sink is enabled. Default: false.
    /// </summary>
    public bool FileEnabled { get; set; } = false;

    /// <summary>
    /// Path for audit log files. Default: logs/audit/
    /// </summary>
    public string FilePath { get; set; } = "logs/audit/";

    /// <summary>
    /// Maximum file size in bytes before rotation. Default: 100MB.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Whether external SIEM sink is enabled. Default: false.
    /// </summary>
    public bool ExternalSinkEnabled { get; set; } = false;

    /// <summary>
    /// URL for external SIEM sink.
    /// </summary>
    public string? ExternalSinkUrl { get; set; }

    /// <summary>
    /// Retention period for audit logs in days. Default: 365.
    /// </summary>
    public int RetentionDays { get; set; } = 365;

    /// <summary>
    /// Whether to include request body in audit logs (for debugging). Default: false.
    /// </summary>
    public bool IncludeRequestBody { get; set; } = false;

    /// <summary>
    /// Fields to redact from audit logs. Default: password, token, key, secret.
    /// </summary>
    public string[] RedactFields { get; set; } = new[]
    {
        "password",
        "token",
        "key",
        "secret",
        "apikey",
        "api_key",
        "authorization",
        "credential",
        "credentials"
    };

    /// <summary>
    /// Buffer size for async audit event processing. Default: 1000.
    /// </summary>
    public int BufferSize { get; set; } = 1000;

    /// <summary>
    /// Batch size for SQL writes. Default: 100.
    /// </summary>
    public int SqlBatchSize { get; set; } = 100;

    /// <summary>
    /// Interval in seconds for batch flush. Default: 5.
    /// </summary>
    public int FlushIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Checks if a given event type should be audited.
    /// </summary>
    public bool ShouldAudit(AuditEventType eventType)
    {
        if (!Enabled) return false;
        if (EnabledEventTypes.Length == 0) return true; // All enabled by default
        return EnabledEventTypes.Contains(eventType);
    }
}
