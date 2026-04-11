using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Orchestration.Capabilities;

/// <summary>
/// Sprint 4: Static factory for warehouse domain capability definitions.
/// Each capability maps a domain operation to a concrete adapter invocation.
/// Stored procedure names follow the ai_ prefix governance convention.
/// </summary>
public static class WarehouseCapabilities
{
    public static IReadOnlyList<CapabilityDescriptor> All { get; } = new[]
    {
        new CapabilityDescriptor
        {
            CapabilityKey = "warehouse.inventory.summary",
            Domain = "warehouse",
            AdapterType = "sql",
            Operation = ToolAdapterOperationNames.ExecuteQuery,
            TargetSystemId = "sql",
            IntegrationBinding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["storedProcedure"] = "dbo.ai_warehouse_inventory_summary"
            },
            ExecutionMode = "readonly"
        },
        new CapabilityDescriptor
        {
            CapabilityKey = "warehouse.inventory.by-item",
            Domain = "warehouse",
            AdapterType = "sql",
            Operation = ToolAdapterOperationNames.ExecuteQuery,
            TargetSystemId = "sql",
            IntegrationBinding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["storedProcedure"] = "dbo.ai_warehouse_inventory_by_item"
            },
            ExecutionMode = "readonly"
        },
        new CapabilityDescriptor
        {
            CapabilityKey = "warehouse.receipts.recent",
            Domain = "warehouse",
            AdapterType = "sql",
            Operation = ToolAdapterOperationNames.ExecuteQuery,
            TargetSystemId = "sql",
            IntegrationBinding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["storedProcedure"] = "dbo.ai_warehouse_receipts_recent"
            },
            ExecutionMode = "readonly"
        },
        new CapabilityDescriptor
        {
            CapabilityKey = "warehouse.external-stock.lookup",
            Domain = "warehouse",
            AdapterType = "rest-json",
            Operation = ToolAdapterOperationNames.ExecuteHttpJson,
            TargetSystemId = "external-stock-api",
            IntegrationBinding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["baseUrl"] = "https://example.invalid",
                ["endpoint"] = "/warehouse/external-stock",
                ["method"] = "GET"
            },
            ExecutionMode = "readonly"
        }
    };
}
