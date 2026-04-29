using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.AiRouting.Tools;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Execution;
using TILSOFTAI.Orchestration.Semantic;
using Xunit;

namespace TILSOFTAI.Tests.AiRouting;

public sealed class OfficialAgentFunctionProviderTests
{
    [Fact]
    public void CapabilityToolDescriptorFactory_ShouldUseSqlBackedLocalizedText()
    {
        var descriptorFactory = CreateDescriptorFactory();
        var capability = Candidate(
            "warehouse.inventory.by-item",
            "read",
            "read",
            description: "Inventory text loaded from SQL.",
            useWhen: "Use when SQL KB says inventory by item is needed.",
            argumentDescription: "SQL-localized item code.",
            aliases: """["mã hàng","sku"]""",
            examples: """["tồn kho CHAIR-001","stock for CHAIR-001"]""").Metadata;

        var descriptor = descriptorFactory.Create(capability);

        descriptor.Name.Should().Be("warehouse_inventory_by_item");
        descriptor.Description.Should().Contain("Inventory text loaded from SQL.");
        descriptor.Description.Should().Contain("Use when SQL KB says inventory by item is needed.");
        descriptor.Description.Should().Contain("SQL-localized item code.");
        descriptor.Description.Should().Contain("Aliases: mã hàng, sku");
        descriptor.Description.Should().Contain("Examples: tồn kho CHAIR-001; stock for CHAIR-001");
        descriptor.ParameterSchema["properties"]!["item_no"]!["description"]!.GetValue<string>()
            .Should().Be("SQL-localized item code.");
    }

    [Fact]
    public async Task OfficialAgentFunctionProvider_ShouldCreateOfficialFunctionAndDispatchThroughFacade()
    {
        var executionFacade = new StubCapabilityExecutionFacade();
        var dynamicFactory = new DynamicFunctionToolFactory(
            CreateDescriptorFactory(),
            executionFacade,
            new StubCompositeCapabilityExecutor());
        var functions = await dynamicFactory.BuildFunctionsAsync(
            [Candidate("model.inventory.by-item", "read", "read", domain: "model")],
            new TilsoftExecutionContext { CorrelationId = "corr-32-3" },
            "en-US",
            CancellationToken.None);

        var function = functions.Should().ContainSingle().Subject;

        var result = await function.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["item_no"] = "CHAIR-001"
            }),
            CancellationToken.None);

        result.Should().BeOfType<CapabilityExecutionEnvelope>();
        function.Name.Should().Be("model_inventory_by_item");
        function.Description.Should().Contain("Loaded from SQL metadata.");
        function.JsonSchema.GetProperty("properties").TryGetProperty("item_no", out _).Should().BeTrue();
        var invocation = function.Should().BeAssignableTo<ICapabilityBackedAIFunction>().Subject.LastInvocation;
        invocation.Should().NotBeNull();
        invocation!.ModelFacingArguments["item_no"]!.GetValue<string>().Should().Be("CHAIR-001");
        executionFacade.LastCapabilityKey.Should().Be("model.inventory.by-item");
        executionFacade.LastArguments.Should().ContainKey("item").WhoseValue.Should().Be("CHAIR-001");
        executionFacade.LastArguments.Should().ContainKey("__functionName").WhoseValue.Should().Be("model_inventory_by_item");
    }

    [Fact]
    public async Task OfficialAgentFunctionProvider_ShouldAllowInvokingNonFirstCandidate()
    {
        var executionFacade = new StubCapabilityExecutionFacade();
        var dynamicFactory = new DynamicFunctionToolFactory(
            CreateDescriptorFactory(),
            executionFacade,
            new StubCompositeCapabilityExecutor());
        var functions = await dynamicFactory.BuildFunctionsAsync(
            [
                Candidate("model.inventory.by-item", "read", "read", domain: "model"),
                Candidate("model.stock.available", "read", "read", domain: "model")
            ],
            new TilsoftExecutionContext { CorrelationId = "corr-32-4" },
            "en-US",
            CancellationToken.None);

        var secondFunction = functions[1];

        var result = await secondFunction.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["item_no"] = "TABLE-002"
            }),
            CancellationToken.None);

        result.Should().BeOfType<CapabilityExecutionEnvelope>();
        functions.Should().HaveCount(2);
        secondFunction.Name.Should().Be("model_stock_available");
        secondFunction.Should().BeAssignableTo<ICapabilityBackedAIFunction>().Subject.LastInvocation.Should().NotBeNull();
        executionFacade.LastCapabilityKey.Should().Be("model.stock.available");
        executionFacade.LastArguments.Should().ContainKey("item").WhoseValue.Should().Be("TABLE-002");
    }

    [Fact]
    public async Task BuildFunctionsAsync_ShouldExposeOnlyReadExecutionCapabilities()
    {
        var factory = CreateFactory();
        var candidates = new[]
        {
            Candidate("model.inventory.by-item", "read", "read", domain: "model"),
            Candidate("sales.order.cancel", "write", "write")
        };

        var functions = await factory.BuildFunctionsAsync(
            candidates,
            new TilsoftExecutionContext(),
            "en-US",
            CancellationToken.None);

        functions.Should().ContainSingle();
        functions[0].Name.Should().Be("model_inventory_by_item");
        var capabilityFunction = functions[0].Should().BeAssignableTo<ICapabilityBackedAIFunction>().Subject;
        capabilityFunction.Descriptor.Capability.CapabilityKey.Should().Be("model.inventory.by-item");
        capabilityFunction.Descriptor.ParameterSchema["properties"]!["item_no"].Should().NotBeNull();
        capabilityFunction.Descriptor.ModelToCapabilityArgumentMap["item_no"].Should().Be("item");
        functions[0].Description.Should().Contain("Business domain: model");
        functions[0].Description.Should().Contain("Aliases: item");
        functions[0].Description.Should().Contain("Examples: stock for CHAIR-001");
    }

    [Fact]
    public async Task BuildFunctionsAsync_ShouldExposeModelMutationAsPreviewOnlyWhenEnabled()
    {
        var executionFacade = new StubCapabilityExecutionFacade();
        var factory = new DynamicFunctionToolFactory(
            CreateDescriptorFactory(),
            executionFacade,
            new StubCompositeCapabilityExecutor(),
            Options.Create(new AiRoutingOptions { EnableWritePreviewTools = true }));

        var functions = await factory.BuildFunctionsAsync(
            [Candidate("model.action.create-preview", "write_preview", "write_preview", domain: "model")],
            new TilsoftExecutionContext { CorrelationId = "corr-32-6" },
            "en-US",
            CancellationToken.None);

        functions.Should().ContainSingle();

        var result = await functions[0].InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?> { ["item_no"] = "SO-001" }),
            CancellationToken.None);

        result.Should().BeOfType<CapabilityExecutionEnvelope>()
            .Which.Status.Should().Be("preview");
        executionFacade.LastPreviewCapabilityKey.Should().Be("model.action.create-preview");
        executionFacade.LastApprovedWriteCapabilityKey.Should().BeNull("model-callable mutation tools must not execute final writes");
        executionFacade.LastArguments.Should().ContainKey("item").WhoseValue.Should().Be("SO-001");
    }

    [Fact]
    public async Task BuildFunctionsAsync_ShouldNotExposeApprovedWriteToolEvenWhenPreviewToolsEnabled()
    {
        var executionFacade = new StubCapabilityExecutionFacade();
        var factory = new DynamicFunctionToolFactory(
            CreateDescriptorFactory(),
            executionFacade,
            new StubCompositeCapabilityExecutor(),
            Options.Create(new AiRoutingOptions { EnableWritePreviewTools = true }));

        var functions = await factory.BuildFunctionsAsync(
            [Candidate("sales.order.create-execute", "write_execute", "write_execute")],
            new TilsoftExecutionContext { CorrelationId = "corr-33-6" },
            "en-US",
            CancellationToken.None);

        functions.Should().BeEmpty("the agent may prepare preview actions but must never see execute-write tools");
    }

    [Fact]
    public async Task BuildFunctionsAsync_ShouldNotAdvertiseNonModelCapabilities()
    {
        var factory = CreateFactory();

        var functions = await factory.BuildFunctionsAsync(
            [
                Candidate("model.count", "read", "read", domain: "model"),
                Candidate("sales.order.status", "read", "read", domain: "sales"),
                Candidate("warehouse.stock.available", "read", "read", domain: "warehouse")
            ],
            new TilsoftExecutionContext(),
            "en-US",
            CancellationToken.None);

        functions.Should().ContainSingle();
        functions[0].Name.Should().Be("model_count");
        functions[0].Should().BeAssignableTo<ICapabilityBackedAIFunction>()
            .Subject.Descriptor.Capability.Domain.Should().Be("model");
    }

    [Fact]
    public async Task BuildFunctionsAsync_ShouldCapAdvertisedModelToolsAtConfiguredLimit()
    {
        var factory = new DynamicFunctionToolFactory(
            CreateDescriptorFactory(),
            new StubCapabilityExecutionFacade(),
            new StubCompositeCapabilityExecutor(),
            Options.Create(new AiRoutingOptions
            {
                MaxCandidateTools = 6,
                MaxTotalCandidateTools = 6
            }));

        var functions = await factory.BuildFunctionsAsync(
            Enumerable.Range(1, 8)
                .Select(index => Candidate($"model.tool-{index}", "read", "read", domain: "model"))
                .ToArray(),
            new TilsoftExecutionContext(),
            "en-US",
            CancellationToken.None);

        functions.Should().HaveCount(6);
        functions.Select(function => function.Name)
            .Should()
            .Equal("model_tool_1", "model_tool_2", "model_tool_3", "model_tool_4", "model_tool_5", "model_tool_6");
    }

    [Fact]
    public async Task BuildFunctionsAsync_ShouldExposeModelCodeInsteadOfModelId()
    {
        var executionFacade = new StubCapabilityExecutionFacade();
        var factory = new DynamicFunctionToolFactory(
            CreateDescriptorFactory(),
            executionFacade,
            new StubCompositeCapabilityExecutor());

        var functions = await factory.BuildFunctionsAsync(
            [ModelCodeCandidate("model.overview.by-code", "modelCode")],
            new TilsoftExecutionContext { CorrelationId = "corr-35-2" },
            "en-US",
            CancellationToken.None);

        var function = functions.Should().ContainSingle().Subject;
        var schema = function.JsonSchema;

        function.Name.Should().Be("model_overview_by_code");
        schema.GetProperty("properties").TryGetProperty("modelCode", out var modelCodeSchema).Should().BeTrue();
        modelCodeSchema.GetProperty("type").GetString().Should().Be("string");
        schema.GetProperty("properties").TryGetProperty("model_id", out _).Should().BeFalse();
        schema.GetProperty("required").EnumerateArray().Select(item => item.GetString())
            .Should()
            .ContainSingle("modelCode");

        await function.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["modelCode"] = "ABC"
            }),
            CancellationToken.None);

        function.Should().BeAssignableTo<ICapabilityBackedAIFunction>()
            .Subject.Descriptor.ModelToCapabilityArgumentMap["modelCode"].Should().Be("modelCode");
        executionFacade.LastCapabilityKey.Should().Be("model.overview.by-code");
        executionFacade.LastArguments.Should().ContainKey("modelCode").WhoseValue.Should().Be("ABC");
        executionFacade.LastArguments.Should().NotContainKey("modelId");
    }

    [Fact]
    public async Task BuildFunctionsAsync_ShouldExposeModelCodesForCompare()
    {
        var factory = CreateFactory();

        var functions = await factory.BuildFunctionsAsync(
            [ModelCodeCandidate("model.compare", "modelCodes", "array")],
            new TilsoftExecutionContext(),
            "en-US",
            CancellationToken.None);

        var function = functions.Should().ContainSingle().Subject;
        var schema = function.JsonSchema;

        schema.GetProperty("properties").TryGetProperty("modelCodes", out var modelCodesSchema).Should().BeTrue();
        modelCodesSchema.GetProperty("type").GetString().Should().Be("array");
        schema.GetProperty("properties").TryGetProperty("model_ids", out _).Should().BeFalse();
        function.Should().BeAssignableTo<ICapabilityBackedAIFunction>()
            .Subject.Descriptor.ModelToCapabilityArgumentMap["modelCodes"].Should().Be("modelCodes");
    }

    private static DynamicFunctionToolFactory CreateFactory() => new(
        CreateDescriptorFactory(),
        new StubCapabilityExecutionFacade(),
        new StubCompositeCapabilityExecutor());

    private static CapabilityToolDescriptorFactory CreateDescriptorFactory() => new(
        new CapabilityToolDescriptionBuilder(),
        new CapabilityParameterSchemaBuilder());

    private static CapabilityCandidate Candidate(
        string key,
        string operation,
        string executionMode,
        string description = "Loaded from SQL metadata.",
        string useWhen = "Use when loaded from SQL.",
        string argumentDescription = "Item code from SQL.",
        string aliases = """["item","sku"]""",
        string examples = """["stock for CHAIR-001"]""",
        string domain = "warehouse") => new()
    {
        Score = 1,
        Metadata = new CapabilitySemanticMetadata
        {
            CapabilityKey = key,
            Domain = domain,
            FunctionName = key.Replace('.', '_').Replace('-', '_'),
            AdapterType = "sql",
            Operation = operation,
            ExecutionMode = executionMode,
            Text = new CapabilityTextMetadata
            {
                Locale = "en-US",
                Description = description,
                UseWhen = useWhen,
                DoNotUseWhen = "Do not use when loaded from C#."
            },
            Arguments =
            [
                new CapabilityArgumentMetadata
                {
                    ArgumentName = "item",
                    ProcParameterName = "@Item",
                    DataType = "string",
                    IsRequired = true,
                    ValidationRule = """{"minLength":1}""",
                    DisplayOrder = 1,
                    Text = new CapabilityArgumentTextMetadata
                    {
                        Locale = "en-US",
                        Description = argumentDescription,
                        Aliases = aliases,
                        Examples = examples
                    }
                }
            ]
        }
    };

    private static CapabilityCandidate ModelCodeCandidate(
        string key,
        string argumentName,
        string dataType = "string") => new()
    {
        Score = 1,
        Metadata = new CapabilitySemanticMetadata
        {
            CapabilityKey = key,
            Domain = "model",
            FunctionName = key.Replace('.', '_').Replace('-', '_'),
            AdapterType = "sql",
            Operation = "read",
            ExecutionMode = "read",
            Text = new CapabilityTextMetadata
            {
                Locale = "en-US",
                Description = "Loaded from SQL metadata.",
                UseWhen = "Use when loaded from SQL.",
                DoNotUseWhen = "Do not use when loaded from C#."
            },
            Arguments =
            [
                new CapabilityArgumentMetadata
                {
                    ArgumentName = argumentName,
                    ProcParameterName = argumentName,
                    DataType = dataType,
                    IsRequired = true,
                    ValidationRule = dataType.Equals("array", StringComparison.OrdinalIgnoreCase)
                        ? """{"minLength":2}"""
                        : """{"minLength":1}""",
                    DisplayOrder = 1,
                    Text = new CapabilityArgumentTextMetadata
                    {
                        Locale = "en-US",
                        Description = dataType.Equals("array", StringComparison.OrdinalIgnoreCase)
                            ? "Business model codes used by users, such as ABC, MD-123, or CHAIR-001."
                            : "Business model code used by users, such as ABC, MD-123, or CHAIR-001.",
                        Aliases = """["model code","model number"]""",
                        Examples = """["ABC","MD-123","CHAIR-001"]"""
                    }
                }
            ]
        }
    };

    private sealed class StubCapabilityExecutionFacade : ICapabilityExecutionFacade
    {
        public string? LastCapabilityKey { get; private set; }
        public string? LastPreviewCapabilityKey { get; private set; }
        public string? LastApprovedWriteCapabilityKey { get; private set; }
        public IReadOnlyDictionary<string, object?> LastArguments { get; private set; } =
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        public Task<CapabilityExecutionEnvelope> ExecuteReadAsync(
            string capabilityKey,
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken)
        {
            LastCapabilityKey = capabilityKey;
            LastArguments = arguments;
            return Task.FromResult(CapabilityExecutionEnvelope.Succeeded(capabilityKey, arguments));
        }

        public Task<CapabilityExecutionEnvelope> PreviewWriteAsync(
            string capabilityKey,
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken)
        {
            LastPreviewCapabilityKey = capabilityKey;
            LastArguments = arguments;
            return Task.FromResult(CapabilityExecutionEnvelope.Succeeded(capabilityKey, arguments) with
            {
                Status = "preview",
                ExecutionMode = "write_preview",
                ExecutionMetadata = ExecutionMetadata.Empty with { Operation = "write_preview" }
            });
        }

        public Task<CapabilityExecutionEnvelope> ExecuteApprovedWriteAsync(
            string capabilityKey,
            string approvedActionId,
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken)
        {
            LastApprovedWriteCapabilityKey = capabilityKey;
            LastArguments = arguments;
            return Task.FromResult(CapabilityExecutionEnvelope.Blocked(capabilityKey, "write blocked"));
        }
    }

    private sealed class StubCompositeCapabilityExecutor : ICompositeCapabilityExecutor
    {
        public Task<CapabilityExecutionEnvelope> ExecuteAsync(
            string compositeCapabilityKey,
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken) =>
            Task.FromResult(CapabilityExecutionEnvelope.Succeeded(compositeCapabilityKey, arguments));
    }
}
