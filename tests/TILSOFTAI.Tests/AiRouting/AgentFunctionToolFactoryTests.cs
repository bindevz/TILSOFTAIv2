using FluentAssertions;
using TILSOFTAI.Orchestration.AiRouting.Tools;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Execution;
using TILSOFTAI.Orchestration.Semantic;
using Xunit;

namespace TILSOFTAI.Tests.AiRouting;

public sealed class AgentFunctionToolFactoryTests
{
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

    private static DynamicFunctionToolFactory CreateFactory() => new(
        new CapabilityToolDescriptionBuilder(),
        new CapabilityParameterSchemaBuilder(),
        new StubCapabilityExecutionFacade(),
        new StubCompositeCapabilityExecutor());

    private static CapabilityCandidate Candidate(string key, string operation, string executionMode) => new()
    {
        Score = 1,
        Metadata = new CapabilitySemanticMetadata
        {
            CapabilityKey = key,
            Domain = "warehouse",
            FunctionName = key == "warehouse.inventory.by-item" ? "warehouse_inventory_by_item" : "execute_tool",
            AdapterType = "sql",
            Operation = operation,
            ExecutionMode = executionMode,
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
                    ArgumentName = "item",
                    ProcParameterName = "@Item",
                    DataType = "string",
                    IsRequired = true,
                    ValidationRule = """{"minLength":1}""",
                    DisplayOrder = 1,
                    Text = new CapabilityArgumentTextMetadata
                    {
                        Locale = "en-US",
                        Description = "Item code from SQL.",
                        Aliases = """["item","sku"]""",
                        Examples = """["stock for CHAIR-001"]"""
                    }
                }
            ]
        }
    };

    private sealed class StubCapabilityExecutionFacade : ICapabilityExecutionFacade
    {
        public Task<CapabilityExecutionEnvelope> ExecuteReadAsync(
            string capabilityKey,
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken) =>
            Task.FromResult(CapabilityExecutionEnvelope.Succeeded(capabilityKey, arguments));

        public Task<CapabilityExecutionEnvelope> PreviewWriteAsync(
            string capabilityKey,
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken) =>
            Task.FromResult(CapabilityExecutionEnvelope.Blocked(capabilityKey, "write blocked"));

        public Task<CapabilityExecutionEnvelope> ExecuteApprovedWriteAsync(
            string capabilityKey,
            string approvedActionId,
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken) =>
            Task.FromResult(CapabilityExecutionEnvelope.Blocked(capabilityKey, "write blocked"));
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
