using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Orchestration.Capabilities;

/// <summary>
/// Sprint 34: model-only read capability definitions for the official agent runtime.
/// </summary>
public static class ModelCapabilities
{
    public static IReadOnlyList<CapabilityDescriptor> All { get; } =
    [
        ReadOnlySql("model.count", "ai_model_count", new CapabilityArgumentContract
        {
            RequiredArguments = [],
            AllowedArguments = ["season"],
            AllowAdditionalArguments = false,
            Arguments =
            [
                new CapabilityArgumentRule
                {
                    Name = "season",
                    Type = "string",
                    Format = "season",
                    MinLength = 1,
                    MaxLength = 50
                }
            ]
        }),
        ReadOnlySql("model.overview.by-code", "ai_model_get_overview", ModelIdContract()),
        ReadOnlySql("model.pieces.by-code", "ai_model_get_pieces", ModelIdContract()),
        ReadOnlySql("model.materials.by-code", "ai_model_get_materials", ModelIdContract()),
        ReadOnlySql("model.compare", "ai_model_compare_models", new CapabilityArgumentContract
        {
            RequiredArguments = ["modelIds"],
            AllowedArguments = ["modelIds"],
            AllowAdditionalArguments = false,
            Arguments =
            [
                new CapabilityArgumentRule
                {
                    Name = "modelIds",
                    Type = "array",
                    Format = "model-id-list",
                    MinLength = 2
                }
            ]
        }),
        ReadOnlySql("model.packaging.by-code", "ai_model_get_packaging", ModelIdContract())
    ];

    private static CapabilityDescriptor ReadOnlySql(
        string capabilityKey,
        string storedProcedure,
        CapabilityArgumentContract argumentContract) => new()
        {
            CapabilityKey = capabilityKey,
            Domain = "model",
            AdapterType = "sql",
            Operation = ToolAdapterOperationNames.ExecuteTool,
            TargetSystemId = "sql",
            IntegrationBinding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["storedProcedure"] = $"dbo.{storedProcedure}"
            },
            RequiredRoles = [],
            ArgumentContract = argumentContract,
            ExecutionMode = "readonly"
        };

    private static CapabilityArgumentContract ModelIdContract() => new()
    {
        RequiredArguments = ["modelId"],
        AllowedArguments = ["modelId"],
        AllowAdditionalArguments = false,
        Arguments =
        [
            new CapabilityArgumentRule
            {
                Name = "modelId",
                Type = "integer",
                Format = "model-id",
                Min = 1
            }
        ]
    };
}
