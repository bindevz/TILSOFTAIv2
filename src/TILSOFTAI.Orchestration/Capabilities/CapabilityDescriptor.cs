namespace TILSOFTAI.Orchestration.Capabilities;

/// <summary>
/// Sprint 4: First-class runtime capability metadata.
/// Describes a single executable capability that an agent can invoke through an adapter.
/// </summary>
public sealed class CapabilityDescriptor
{
    /// <summary>
    /// Unique key identifying this capability (e.g. "warehouse.inventory.summary").
    /// </summary>
    public string CapabilityKey { get; init; } = string.Empty;

    /// <summary>
    /// Domain this capability belongs to (e.g. "warehouse", "accounting").
    /// </summary>
    public string Domain { get; init; } = string.Empty;

    /// <summary>
    /// Adapter type that executes this capability (e.g. "sql", "rest", "queue").
    /// </summary>
    public string AdapterType { get; init; } = string.Empty;

    /// <summary>
    /// Operation name used by the adapter (e.g. "execute_query", "execute_tool").
    /// </summary>
    public string Operation { get; init; } = string.Empty;

    /// <summary>
    /// Target system identifier for adapter resolution (e.g. "sql").
    /// </summary>
    public string TargetSystemId { get; init; } = string.Empty;

    /// <summary>
    /// Adapter-specific integration binding metadata.
    /// For SQL: { "storedProcedure": "dbo.ai_warehouse_inventory_summary" }
    /// For REST: { "endpoint": "/api/warehouse/inventory", "method": "GET" }
    /// </summary>
    public IReadOnlyDictionary<string, string> IntegrationBinding { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Role names required to execute this capability. Empty means no capability-specific role gate.
    /// </summary>
    public IReadOnlyList<string> RequiredRoles { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Tenant ids allowed to execute this capability. Empty means all tenants are allowed.
    /// </summary>
    public IReadOnlyList<string> AllowedTenants { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Execution mode: "readonly" or "write".
    /// Write capabilities must route through ApprovalEngine.
    /// </summary>
    public string ExecutionMode { get; init; } = "readonly";
}
