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
            RequiredRoles = new[] { "warehouse_read" },
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
            RequiredRoles = new[] { "warehouse_read" },
            ArgumentContract = new CapabilityArgumentContract
            {
                RequiredArguments = new[] { "@ItemNo" },
                AllowedArguments = new[] { "@ItemNo", "@TenantId" },
                AllowAdditionalArguments = false,
                Arguments = new[]
                {
                    new CapabilityArgumentRule
                    {
                        Name = "@ItemNo",
                        Type = "string",
                        Format = "item-number",
                        MinLength = 1,
                        MaxLength = 50
                    },
                    new CapabilityArgumentRule
                    {
                        Name = "@TenantId",
                        Type = "string",
                        Format = "tenant-id",
                        MinLength = 1,
                        MaxLength = 80
                    }
                }
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
            RequiredRoles = new[] { "warehouse_read" },
            ExecutionMode = "readonly"
        },
        new CapabilityDescriptor
        {
            CapabilityKey = "warehouse.external-stock.lookup",
            Domain = "warehouse",
            AdapterType = "rest-json",
            Operation = ToolAdapterOperationNames.ExecuteHttpJson,
            TargetSystemId = "external-stock-api",
            RequiredRoles = new[] { "warehouse_external_read" },
            ArgumentContract = new CapabilityArgumentContract
            {
                RequiredArguments = new[] { "@ItemNo" },
                AllowedArguments = new[] { "@ItemNo" },
                AllowAdditionalArguments = false,
                Arguments = new[]
                {
                    new CapabilityArgumentRule
                    {
                        Name = "@ItemNo",
                        Type = "string",
                        Format = "item-number",
                        MinLength = 1,
                        MaxLength = 50
                    }
                }
            },
            ExecutionMode = "readonly"
        }
    };
}
