using FluentAssertions;
using TILSOFTAI.Orchestration.AiRouting.Tools;
using TILSOFTAI.Orchestration.Semantic;
using Xunit;

namespace TILSOFTAI.Tests.AiRouting;

public sealed class CapabilityToolDescriptionBuilderTests
{
    [Fact]
    public void DynamicFunctionToolFactory_UsesSqlDescriptions()
    {
        var metadata = new CapabilitySemanticMetadata
        {
            CapabilityKey = "model.materials.by-code",
            Domain = "model",
            FunctionName = "model_materials_by_code",
            AdapterType = "sql",
            Operation = "execute_tool",
            StoredProcedure = "ai_model_get_materials",
            ExecutionMode = "readonly",
            Text = new CapabilityTextMetadata
            {
                Locale = "en-US",
                ShortName = "Model materials",
                Description = "SQL description: return material rows for one model code.",
                DoNotUseWhen = "SQL negative description: not for packaging.",
                Aliases = """["materials","raw materials"]"""
            },
            Arguments =
            [
                new CapabilityArgumentMetadata
                {
                    ArgumentName = "modelCode",
                    ProcParameterName = "modelCode",
                    DataType = "string",
                    IsRequired = true,
                    DisplayOrder = 1,
                    Text = new CapabilityArgumentTextMetadata
                    {
                        Locale = "en-US",
                        Description = "SQL argument text: model code.",
                        Aliases = """["model","sku"]""",
                        Examples = """["ABC"]""",
                        ClarificationQuestion = "Which model code should I inspect?"
                    }
                }
            ],
            Examples =
            [
                new CapabilityExampleMetadata
                {
                    Locale = "en-US",
                    Utterance = "Show materials for model ABC",
                    ArgumentsJson = """{"modelCode":"ABC"}""",
                    SortOrder = 1
                }
            ]
        };

        var description = new CapabilityToolDescriptionBuilder().Build(metadata);

        description.Should().Contain("SQL description");
        description.Should().Contain("SQL negative description");
        description.Should().Contain("raw materials");
        description.Should().Contain("Show materials for model ABC");
        description.Should().Contain("SQL argument text");
        description.Should().Contain("Which model code should I inspect?");
        description.Should().NotContain("ai_model_get_materials");
    }
}
