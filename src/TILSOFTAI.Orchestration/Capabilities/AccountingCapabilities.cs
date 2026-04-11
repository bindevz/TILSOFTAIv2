using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Orchestration.Capabilities;

/// <summary>
/// Sprint 5: Static factory for accounting domain capability definitions.
/// Each capability maps a domain operation to a concrete adapter invocation.
/// Stored procedure names follow the ai_ prefix governance convention.
/// </summary>
public static class AccountingCapabilities
{
    public static IReadOnlyList<CapabilityDescriptor> All { get; } = new[]
    {
        new CapabilityDescriptor
        {
            CapabilityKey = "accounting.receivables.summary",
            Domain = "accounting",
            AdapterType = "sql",
            Operation = ToolAdapterOperationNames.ExecuteQuery,
            TargetSystemId = "sql",
            IntegrationBinding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["storedProcedure"] = "dbo.ai_accounting_receivables_summary"
            },
            RequiredRoles = new[] { "accounting_read" },
            ExecutionMode = "readonly"
        },
        new CapabilityDescriptor
        {
            CapabilityKey = "accounting.payables.summary",
            Domain = "accounting",
            AdapterType = "sql",
            Operation = ToolAdapterOperationNames.ExecuteQuery,
            TargetSystemId = "sql",
            IntegrationBinding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["storedProcedure"] = "dbo.ai_accounting_payables_summary"
            },
            RequiredRoles = new[] { "accounting_read" },
            ExecutionMode = "readonly"
        },
        new CapabilityDescriptor
        {
            CapabilityKey = "accounting.invoice.by-number",
            Domain = "accounting",
            AdapterType = "sql",
            Operation = ToolAdapterOperationNames.ExecuteQuery,
            TargetSystemId = "sql",
            IntegrationBinding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["storedProcedure"] = "dbo.ai_accounting_invoice_by_number"
            },
            RequiredRoles = new[] { "accounting_read" },
            ArgumentContract = new CapabilityArgumentContract
            {
                RequiredArguments = new[] { "@InvoiceNumber" },
                AllowedArguments = new[] { "@InvoiceNumber", "@TenantId" },
                AllowAdditionalArguments = false,
                Arguments = new[]
                {
                    new CapabilityArgumentRule
                    {
                        Name = "@InvoiceNumber",
                        Type = "string",
                        Format = "invoice-number",
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
            CapabilityKey = "accounting.exchange-rate.lookup",
            Domain = "accounting",
            AdapterType = "rest-json",
            Operation = ToolAdapterOperationNames.ExecuteHttpJson,
            TargetSystemId = "fx-rate-api",
            RequiredRoles = new[] { "accounting_external_read" },
            ArgumentContract = new CapabilityArgumentContract
            {
                RequiredArguments = new[] { "@CurrencyCode" },
                AllowedArguments = new[] { "@CurrencyCode" },
                AllowAdditionalArguments = false,
                Arguments = new[]
                {
                    new CapabilityArgumentRule
                    {
                        Name = "@CurrencyCode",
                        Type = "string",
                        Format = "currency-code",
                        Enum = new[] { "USD", "EUR", "VND", "THB" }
                    }
                }
            },
            ExecutionMode = "readonly"
        }
    };
}
