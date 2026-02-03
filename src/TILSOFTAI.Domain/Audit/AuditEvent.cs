using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TILSOFTAI.Domain.Audit;

/// <summary>
/// Base class for all audit events. Captures full context for compliance reporting.
/// </summary>
public class AuditEvent
{
    /// <summary>
    /// Unique identifier for this audit event.
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Type of security event.
    /// </summary>
    public AuditEventType EventType { get; init; }

    /// <summary>
    /// UTC timestamp when the event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Tenant identifier.
    /// </summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>
    /// User identifier who performed the action.
    /// </summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// Correlation ID for request tracing.
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// Client IP address.
    /// </summary>
    public string IpAddress { get; init; } = string.Empty;

    /// <summary>
    /// Client User-Agent header.
    /// </summary>
    public string UserAgent { get; init; } = string.Empty;

    /// <summary>
    /// Outcome of the operation.
    /// </summary>
    public AuditOutcome Outcome { get; init; } = AuditOutcome.Success;

    /// <summary>
    /// Event-specific details as JSON.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonDocument? Details { get; init; }

    /// <summary>
    /// SHA256 checksum of the serialized event for tamper detection.
    /// </summary>
    public string Checksum { get; private set; } = string.Empty;

    /// <summary>
    /// Calculates and sets the checksum for this event.
    /// </summary>
    public void ComputeChecksum()
    {
        var json = JsonSerializer.Serialize(new
        {
            EventId,
            EventType,
            Timestamp,
            TenantId,
            UserId,
            CorrelationId,
            IpAddress,
            UserAgent,
            Outcome,
            Details = Details?.RootElement.GetRawText()
        });

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        Checksum = Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Verifies the checksum matches the event data.
    /// </summary>
    public bool VerifyChecksum()
    {
        var originalChecksum = Checksum;
        ComputeChecksum();
        var isValid = Checksum == originalChecksum;
        Checksum = originalChecksum;
        return isValid;
    }
}
