using TILSOFTAI.Modules.Model;
using TILSOFTAI.Orchestration.Tools;
using Xunit;

namespace TILSOFTAI.Tests.Contract.Validation;

public sealed class JsonSchemaValidatorTests
{
    [Fact]
    public void JsonSchemaValidator_MissingRequiredModelId_IsInvalid()
    {
        var validator = new RealJsonSchemaValidator();
        var schema = GetModelOverviewSchema();

        var result = validator.Validate(schema, "{}");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void JsonSchemaValidator_ModelIdWrongType_IsInvalid()
    {
        var validator = new RealJsonSchemaValidator();
        var schema = GetModelOverviewSchema();

        var result = validator.Validate(schema, "{\"modelId\":\"abc\"}");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void JsonSchemaValidator_ExtraProperty_IsInvalid()
    {
        var validator = new RealJsonSchemaValidator();
        var schema = GetModelOverviewSchema();

        var result = validator.Validate(schema, "{\"modelId\":1,\"extra\":true}");

        Assert.False(result.IsValid);
    }

    private static string GetModelOverviewSchema()
    {
        var registry = new ToolRegistry();
        var handlerRegistry = new NamedToolHandlerRegistry();
        var module = new ModelModule();
        module.Register(registry, handlerRegistry);

        return registry.Get("model_get_overview").JsonSchema;
    }
}
