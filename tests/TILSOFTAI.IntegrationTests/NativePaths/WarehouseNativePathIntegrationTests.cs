using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TILSOFTAI.Agents;
using TILSOFTAI.Agents.Abstractions;
using TILSOFTAI.Agents.Domain;
using TILSOFTAI.Approvals;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Supervisor;
using TILSOFTAI.Supervisor.Classification;
using TILSOFTAI.Tools.Abstractions;
using Xunit;

namespace TILSOFTAI.IntegrationTests.NativePaths;

/// <summary>
/// Sprint 5: Integration tests for the Warehouse native execution path.
/// Validates non-regression of Sprint 4 warehouse native path after Sprint 5 changes.
/// Uses fully-wired runtime with stub adapter.
/// </summary>
public sealed class WarehouseNativePathIntegrationTests
{
    private static LegacyChatPipelineBridge CreateUninitializedBridge() =>
        (LegacyChatPipelineBridge)RuntimeHelpers.GetUninitializedObject(typeof(LegacyChatPipelineBridge));

    private static (SupervisorRuntime runtime, Mock<IToolAdapter> stubAdapter) BuildRuntime()
    {
        var stubAdapter = new Mock<IToolAdapter>();
        stubAdapter.Setup(a => a.AdapterType).Returns("sql");
        stubAdapter.Setup(a => a.ExecuteAsync(It.IsAny<ToolExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.Ok("{\"items\": 42}"));

        var adapterRegistry = new ToolAdapterRegistry(new[] { stubAdapter.Object });

        var capabilityResolver = new StructuredCapabilityResolver(
            new Mock<ILogger<StructuredCapabilityResolver>>().Object);

        var capabilityRegistry = new InMemoryCapabilityRegistry(
            WarehouseCapabilities.All.Concat(AccountingCapabilities.All));

        var warehouseAgent = new WarehouseAgent(
            CreateUninitializedBridge(),
            capabilityRegistry,
            capabilityResolver,
            new Mock<ILogger<WarehouseAgent>>().Object);

        var accountingAgent = new AccountingAgent(
            CreateUninitializedBridge(),
            capabilityRegistry,
            capabilityResolver,
            new Mock<ILogger<AccountingAgent>>().Object);

        var agents = new IDomainAgent[] { warehouseAgent, accountingAgent };
        var agentRegistry = new DomainAgentRegistry(
            agents, new Mock<ILogger<DomainAgentRegistry>>().Object);

        var classifier = new KeywordIntentClassifier(
            new Mock<ILogger<KeywordIntentClassifier>>().Object);

        var approvalEngine = new Mock<IApprovalEngine>().Object;

        var runtime = new SupervisorRuntime(
            classifier,
            agentRegistry,
            approvalEngine,
            adapterRegistry,
            new Mock<ILogger<SupervisorRuntime>>().Object);

        return (runtime, stubAdapter);
    }

    [Fact]
    public async Task WarehouseRequest_ShouldRouteToWarehouseAgent_AndExecuteNatively()
    {
        var (runtime, stubAdapter) = BuildRuntime();

        var request = new SupervisorRequest
        {
            Input = "show me warehouse inventory summary"
        };

        var ctx = new TilsoftExecutionContext
        {
            TenantId = "tenant-wh",
            UserId = "user-wh",
            CorrelationId = "corr-wh",
            Roles = new[] { "warehouse_read" }
        };

        var result = await runtime.RunAsync(request, ctx, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SelectedAgentId.Should().Be("warehouse");
        result.Output.Should().Contain("42");

        stubAdapter.Verify(a => a.ExecuteAsync(
            It.Is<ToolExecutionRequest>(r =>
                r.CapabilityKey.StartsWith("warehouse.") &&
                r.AgentId == "warehouse" &&
                r.TenantId == "tenant-wh"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WarehouseRequest_WithExplicitCapabilityKey_ShouldResolveExactly()
    {
        var (runtime, stubAdapter) = BuildRuntime();

        var metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["capabilityKey"] = "warehouse.receipts.recent"
        };

        var request = new SupervisorRequest
        {
            Input = "warehouse receipts",
            Metadata = metadata
        };

        var ctx = new TilsoftExecutionContext
        {
            TenantId = "tenant-explicit",
            CorrelationId = "corr-explicit",
            Roles = new[] { "warehouse_read" }
        };

        var result = await runtime.RunAsync(request, ctx, CancellationToken.None);

        result.Success.Should().BeTrue();

        stubAdapter.Verify(a => a.ExecuteAsync(
            It.Is<ToolExecutionRequest>(r =>
                r.CapabilityKey == "warehouse.receipts.recent" &&
                r.Metadata.ContainsKey("storedProcedure")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WarehouseAgent_ShouldNotRegress_AfterSprint5Changes()
    {
        var (runtime, stubAdapter) = BuildRuntime();

        // All three warehouse capabilities should still be reachable
        var inputs = new[]
        {
            ("show me inventory summary for warehouse", "warehouse.inventory.summary"),
            ("warehouse inventory by item details", "warehouse.inventory.by-item"),
        };

        foreach (var (input, expectedPrefix) in inputs)
        {
            var result = await runtime.RunAsync(
                new SupervisorRequest { Input = input },
                new TilsoftExecutionContext { TenantId = "t1", CorrelationId = "c1", Roles = new[] { "warehouse_read" } },
                CancellationToken.None);

            result.Success.Should().BeTrue($"Input '{input}' should succeed");
            result.SelectedAgentId.Should().Be("warehouse", $"Input '{input}' should route to warehouse");
        }
    }
}
