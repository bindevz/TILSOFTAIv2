using TILSOFTAI.Orchestration.Modules;
using TILSOFTAI.Orchestration.Tools;
using TILSOFTAI.Modules.Model.Tools;

namespace TILSOFTAI.Modules.Model;

public sealed class ModelModule : ITilsoftModule
{
    public string Name => "Model";

    public void Register(IToolRegistry toolRegistry, INamedToolHandlerRegistry handlerRegistry)
    {
        if (toolRegistry is null)
        {
            throw new ArgumentNullException(nameof(toolRegistry));
        }

        if (handlerRegistry is null)
        {
            throw new ArgumentNullException(nameof(handlerRegistry));
        }

        toolRegistry.Register(new ToolDefinition
        {
            Name = "model_get_overview",
            Description = "Get model overview, logistics metrics, and piece counts.",
            Instruction = Instructions.Overview,
            JsonSchema = Schemas.ModelIdSchema,
            SpName = "ai_model_get_overview",
            RequiredRoles = Array.Empty<string>(),
            Module = Name,
            IsEnabled = true
        });

        toolRegistry.Register(new ToolDefinition
        {
            Name = "model_get_pieces",
            Description = "List model pieces and nested model references.",
            Instruction = Instructions.Pieces,
            JsonSchema = Schemas.ModelIdSchema,
            SpName = "ai_model_get_pieces",
            RequiredRoles = Array.Empty<string>(),
            Module = Name,
            IsEnabled = true
        });

        toolRegistry.Register(new ToolDefinition
        {
            Name = "model_get_materials",
            Description = "Get model material configuration and quantities.",
            Instruction = Instructions.Materials,
            JsonSchema = Schemas.ModelIdSchema,
            SpName = "ai_model_get_materials",
            RequiredRoles = Array.Empty<string>(),
            Module = Name,
            IsEnabled = true
        });

        toolRegistry.Register(new ToolDefinition
        {
            Name = "model_compare_models",
            Description = "Compare models across logistics and packaging metrics.",
            Instruction = Instructions.Compare,
            JsonSchema = Schemas.ModelIdsSchema,
            SpName = "ai_model_compare_models",
            RequiredRoles = Array.Empty<string>(),
            Module = Name,
            IsEnabled = true
        });

        toolRegistry.Register(new ToolDefinition
        {
            Name = "model_get_packaging",
            Description = "Get default packaging metrics for a model.",
            Instruction = Instructions.Packaging,
            JsonSchema = Schemas.ModelIdSchema,
            SpName = "ai_model_get_packaging",
            RequiredRoles = Array.Empty<string>(),
            Module = Name,
            IsEnabled = true
        });

        toolRegistry.Register(new ToolDefinition
        {
            Name = "model_count",
            Description = "Count models by season.",
            Instruction = Instructions.Count,
            JsonSchema = Schemas.ModelCountSchema,
            SpName = "ai_model_count",
            RequiredRoles = Array.Empty<string>(),
            Module = Name,
            IsEnabled = true
        });

        handlerRegistry.Register("model_get_overview", typeof(ModelGetOverviewToolHandler));
        handlerRegistry.Register("model_get_pieces", typeof(ModelGetPiecesToolHandler));
        handlerRegistry.Register("model_get_materials", typeof(ModelGetMaterialsToolHandler));
        handlerRegistry.Register("model_compare_models", typeof(ModelCompareModelsToolHandler));
        handlerRegistry.Register("model_get_packaging", typeof(ModelGetPackagingToolHandler));
        handlerRegistry.Register("model_count", typeof(ModelCountToolHandler));
    }

    private static class Schemas
    {
        public const string ModelIdSchema = "{\"type\":\"object\",\"required\":[\"modelId\"],\"properties\":{\"modelId\":{\"type\":\"integer\",\"minimum\":1}},\"additionalProperties\":false}";
        public const string ModelIdsSchema = "{\"type\":\"object\",\"required\":[\"modelIds\"],\"properties\":{\"modelIds\":{\"type\":\"array\",\"minItems\":2,\"items\":{\"type\":\"integer\",\"minimum\":1}}},\"additionalProperties\":false}";
        public const string ModelCountSchema = "{\"type\":\"object\",\"properties\":{\"season\":{\"type\":\"string\"}},\"additionalProperties\":false}";
    }

    private static class Instructions
    {
        public const string Overview = "Call when the user asks for a model summary or packaging/logistics metrics. Interpret DefaultCbm (Model.Cbm), Qnt40HC (Model.Qnt40HC), BoxInSet (Packaging.BoxInSet), and FSC/RCS flags. Follow-up: if PieceCount > 0 call model_get_pieces; if materials are needed call model_get_materials; if packaging details are needed call model_get_packaging; if comparing multiple models call model_compare_models. Safety: use only the provided modelId; avoid PII; keep scope tight.";
        public const string Pieces = "Call when you need the piece list or hierarchy for a model. Use ChildModelId to detect nested sets and recursively call model_get_pieces for child model ids until the recursion policy limit. Follow-up: if materials are requested for a piece model, call model_get_materials with that child model id. Safety: do not traverse beyond max recursion depth.";
        public const string Materials = "Call when the user asks for material composition or sustainability flags. Group results by ProductWizardSectionNM and cite IsFSCEnabled/IsRCSEnabled with MaterialGroupID. Follow-up: use model_compare_models for cross-model comparisons. Safety: avoid inference; rely on returned metrics.";
        public const string Compare = "Call when the user asks to compare two or more models (packaging or loadability differences). Explain differences using DefaultCbm, Qnt40HC, BoxInSet, and CbmPer40HC; note FSC/RCS flags when relevant. Follow-up: if needed, drill into pieces, materials, or packaging for each model. Safety: do not speculate; stick to computed values.";
        public const string Packaging = "Call when the user asks for packaging method, carton dimensions, CBM, BoxInSet, or container quantities. Uses the default packaging option for the model. Safety: use only the provided modelId; avoid PII.";
        public const string Count = "Call when the user asks for model counts overall or by season. Optionally pass season to filter; results return counts grouped by season.";
    }
}
