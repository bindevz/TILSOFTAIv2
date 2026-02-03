using TILSOFTAI.Domain.Audit;

namespace TILSOFTAI.Infrastructure.Audit;

/// <summary>
/// Interface for audit event sinks (SQL, File, External).
/// </summary>
public interface IAuditSink
{
    /// <summary>
    /// Name of the sink for logging purposes.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether this sink is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Writes a batch of audit events to the sink.
    /// </summary>
    Task WriteBatchAsync(IReadOnlyList<AuditEvent> events, CancellationToken ct);
}
