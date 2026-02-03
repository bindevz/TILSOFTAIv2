namespace TILSOFTAI.Domain.Audit;

/// <summary>
/// Audit event for data access operations.
/// </summary>
public sealed class DataAccessAuditEvent : AuditEvent
{
    /// <summary>
    /// Type of entity being accessed (Conversation, ToolExecution, etc.).
    /// </summary>
    public string EntityType { get; init; } = string.Empty;

    /// <summary>
    /// Identifier of the entity.
    /// </summary>
    public string EntityId { get; init; } = string.Empty;

    /// <summary>
    /// Type of data operation performed.
    /// </summary>
    public DataOperation Operation { get; init; }

    /// <summary>
    /// Fields affected by update operations.
    /// </summary>
    public string[] AffectedFields { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Number of records affected for bulk operations.
    /// </summary>
    public int RecordCount { get; init; } = 1;

    /// <summary>
    /// Creates a read access event.
    /// </summary>
    public static DataAccessAuditEvent Read(
        string tenantId,
        string userId,
        string correlationId,
        string ipAddress,
        string userAgent,
        string entityType,
        string entityId,
        int recordCount = 1)
    {
        return new DataAccessAuditEvent
        {
            EventType = AuditEventType.DataAccess_Read,
            TenantId = tenantId,
            UserId = userId,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Outcome = AuditOutcome.Success,
            EntityType = entityType,
            EntityId = entityId,
            Operation = DataOperation.Read,
            RecordCount = recordCount
        };
    }

    /// <summary>
    /// Creates a write access event.
    /// </summary>
    public static DataAccessAuditEvent Write(
        string tenantId,
        string userId,
        string correlationId,
        string ipAddress,
        string userAgent,
        string entityType,
        string entityId,
        DataOperation operation,
        string[]? affectedFields = null,
        int recordCount = 1)
    {
        var eventType = operation switch
        {
            DataOperation.Create => AuditEventType.DataAccess_Write,
            DataOperation.Update => AuditEventType.DataAccess_Write,
            DataOperation.Delete => AuditEventType.DataAccess_Delete,
            _ => AuditEventType.DataAccess_Write
        };

        return new DataAccessAuditEvent
        {
            EventType = eventType,
            TenantId = tenantId,
            UserId = userId,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Outcome = AuditOutcome.Success,
            EntityType = entityType,
            EntityId = entityId,
            Operation = operation,
            AffectedFields = affectedFields ?? Array.Empty<string>(),
            RecordCount = recordCount
        };
    }
}
