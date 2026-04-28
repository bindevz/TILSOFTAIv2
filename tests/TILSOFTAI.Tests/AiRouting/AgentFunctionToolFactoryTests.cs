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

public sealed class AgentFunctionToolFactoryTests
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
    public async Task OfficialAgentToolFactory_ShouldCreateOfficialFunctionAndDispatchThroughFacade()
    {
        var executionFacade = new StubCapabilityExecutionFacade();
        var dynamicFactory = new DynamicFunctionToolFactory(
            CreateDescriptorFactory(),
            executionFacade,
            new StubCompositeCapabilityExecutor());
        var tools = await dynamicFactory.BuildToolsAsync(
            [Candidate("warehouse.inventory.by-item", "read", "read")],
            new TilsoftExecutionContext { CorrelationId = "corr-32-3" },
            "en-US",
            CancellationToken.None);

        var invocations = new List<OfficialAgentToolInvocation>();
        var officialTool = new OfficialAgentToolFactory()
            .CreateTools(tools, invocations.Add)
            .Should()
            .ContainSingle()
            .Subject;
        var function = officialTool.Tool.Should().BeAssignableTo<AIFunction>().Subject;

        var result = await function.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["item_no"] = "CHAIR-001"
            }),
            CancellationToken.None);

        result.Should().BeOfType<CapabilityExecutionEnvelope>();
        function.Name.Should().Be("warehouse_inventory_by_item");
        function.Description.Should().Contain("Loaded from SQL metadata.");
        function.JsonSchema.GetProperty("properties").TryGetProperty("item_no", out _).Should().BeTrue();
        invocations.Should().ContainSingle();
        invocations[0].ModelFacingArguments["item_no"]!.GetValue<string>().Should().Be("CHAIR-001");
        executionFacade.LastCapabilityKey.Should().Be("warehouse.inventory.by-item");
        executionFacade.LastArguments.Should().ContainKey("item").WhoseValue.Should().Be("CHAIR-001");
        executionFacade.LastArguments.Should().ContainKey("__functionName").WhoseValue.Should().Be("warehouse_inventory_by_item");
    }

    [Fact]
    public async Task OfficialAgentToolFactory_ShouldAllowInvokingNonFirstCandidate()
    {
        var executionFacade = new StubCapabilityExecutionFacade();
        var dynamicFactory = new DynamicFunctionToolFactory(
            CreateDescriptorFactory(),
            executionFacade,
            new StubCompositeCapabilityExecutor());
        var tools = await dynamicFactory.BuildToolsAsync(
            [
                Candidate("warehouse.inventory.by-item", "read", "read"),
                Candidate("warehouse.stock.available", "read", "read")
            ],
            new TilsoftExecutionContext { CorrelationId = "corr-32-4" },
            "en-US",
            CancellationToken.None);

        var invocations = new List<OfficialAgentToolInvocation>();
        var officialTools = new OfficialAgentToolFactory()
            .CreateTools(tools, invocations.Add);
        var secondFunction = officialTools[1].Tool.Should().BeAssignableTo<AIFunction>().Subject;

        var result = await secondFunction.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["item_no"] = "TABLE-002"
            }),
            CancellationToken.None);

        result.Should().BeOfType<CapabilityExecutionEnvelope>();
        officialTools.Should().HaveCount(2);
        secondFunction.Name.Should().Be("warehouse_stock_available");
        invocations.Should().ContainSingle();
        executionFacade.LastCapabilityKey.Should().Be("warehouse.stock.available");
        executionFacade.LastArguments.Should().ContainKey("item").WhoseValue.Should().Be("TABLE-002");
    }

    [Fact]
    public async Task BuildToolsAsync_ShouldExposeOnlyReadExecutionCapabilities()
    {
        var factory = CreateFactory();
        var candidates = new[]
        {
            Candidate("warehouse.inventory.by-item", "read", "read"),
            Candidate("sales.order.cancel", "write", "write")
        };

        var tools = await factory.BuildToolsAsync(
            candidates,
            new TilsoftExecutionContext(),
            "en-US",
            CancellationToken.None);

        tools.Should().ContainSingle();
        tools[0].Name.Should().Be("warehouse_inventory_by_item");
        tools[0].Capability.CapabilityKey.Should().Be("warehouse.inventory.by-item");
        tools[0].ParameterSchema["properties"]!["item_no"].Should().NotBeNull();
        tools[0].ModelToCapabilityArgumentMap["item_no"].Should().Be("item");
        tools[0].Description.Should().Contain("Business domain: warehouse");
        tools[0].Description.Should().Contain("Aliases: item");
        tools[0].Description.Should().Contain("Examples: stock for CHAIR-001");
    }

    [Fact]
    public async Task BuildToolsAsync_ShouldExposeMutationAsPreviewOnlyWhenEnabled()
    {
        var executionFacade = new StubCapabilityExecutionFacade();
        var factory = new DynamicFunctionToolFactory(
            CreateDescriptorFactory(),
            executionFacade,
            new StubCompositeCapabilityExecutor(),
            Options.Create(new AiRoutingOptions { EnableWritePreviewTools = true }));

        var tools = await factory.BuildToolsAsync(
            [Candidate("sales.order.cancel", "write", "write_execute")],
            new TilsoftExecutionContext { CorrelationId = "corr-32-6" },
            "en-US",
            CancellationToken.None);

        tools.Should().ContainSingle();

        var result = await tools[0].InvokeAsync(
            new Dictionary<string, object?> { ["item_no"] = "SO-001" },
            CancellationToken.None);

        result.Status.Should().Be("preview");
        executionFacade.LastPreviewCapabilityKey.Should().Be("sales.order.cancel");
        executionFacade.LastApprovedWriteCapabilityKey.Should().BeNull("model-callable mutation tools must not execute final writes");
        executionFacade.LastArguments.Should().ContainKey("item").WhoseValue.Should().Be("SO-001");
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
        string examples = """["stock for CHAIR-001"]""") => new()
    {
        Score = 1,
        Metadata = new CapabilitySemanticMetadata
        {
            CapabilityKey = key,
            Domain = "warehouse",
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
