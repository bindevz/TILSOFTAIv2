using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Tests.Fixtures;

public static class ModelCapabilityFixtures
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
        ReadOnlySql("model.overview.by-code", "ai_model_get_overview", ModelCodeContract()),
        ReadOnlySql("model.pieces.by-code", "ai_model_get_pieces", ModelCodeContract()),
        ReadOnlySql("model.materials.by-code", "ai_model_get_materials", ModelCodeContract()),
        ReadOnlySql("model.compare", "ai_model_compare", new CapabilityArgumentContract
        {
            RequiredArguments = ["modelCodes"],
            AllowedArguments = ["modelCodes"],
            AllowAdditionalArguments = false,
            Arguments =
            [
                new CapabilityArgumentRule
                {
                    Name = "modelCodes",
                    Type = "array",
                    Format = "model-code-list",
                    MinLength = 2
                }
            ]
        }),
        ReadOnlySql("model.packaging.by-code", "ai_model_get_packaging", ModelCodeContract())
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
                ["storedProcedure"] = storedProcedure
            },
            RequiredRoles = [],
            ArgumentContract = argumentContract,
            ExecutionMode = "readonly"
        };

    private static CapabilityArgumentContract ModelCodeContract() => new()
    {
        RequiredArguments = ["modelCode"],
        AllowedArguments = ["modelCode"],
        AllowAdditionalArguments = false,
        Arguments =
        [
            new CapabilityArgumentRule
            {
                Name = "modelCode",
                Type = "string",
                Format = "model-code",
                MinLength = 1,
                MaxLength = 50
            }
        ]
    };
}
