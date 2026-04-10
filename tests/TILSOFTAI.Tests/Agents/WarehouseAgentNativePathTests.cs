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
using TILSOFTAI.Tools.Abstractions;
using Xunit;

namespace TILSOFTAI.Tests.Agents;

public sealed class WarehouseAgentNativePathTests
{
    /// <summary>
    /// Creates a LegacyChatPipelineBridge without calling its constructor.
    /// This avoids the null-check on ChatPipeline. The instance is only valid
    /// for native-path tests where Bridge.ExecuteAsync is never called.
    /// </summary>
    private static LegacyChatPipelineBridge CreateUninitializedBridge() =>
        (LegacyChatPipelineBridge)RuntimeHelpers.GetUninitializedObject(typeof(LegacyChatPipelineBridge));

    private static WarehouseAgent CreateAgent(ICapabilityRegistry? capabilityRegistry = null)
    {
        var capReg = capabilityRegistry ?? new InMemoryCapabilityRegistry(WarehouseCapabilities.All);
        var logger = new Mock<ILogger<WarehouseAgent>>().Object;
        return new WarehouseAgent(CreateUninitializedBridge(), capReg, logger);
    }

    private static AgentExecutionContext CreateContext(IToolAdapterRegistry? adapterRegistry = null)
    {
        return new AgentExecutionContext
        {
            RuntimeContext = new TilsoftExecutionContext
            {
                TenantId = "tenant-1",
                UserId = "user-1",
                CorrelationId = "corr-1"
            },
            ApprovalEngine = new Mock<IApprovalEngine>().Object,
            ToolAdapterRegistry = adapterRegistry
        };
    }

    // ────────────────────────── Identity ──────────────────────────

    [Fact]
    public void AgentId_ShouldBeWarehouse()
    {
        var agent = CreateAgent();
        agent.AgentId.Should().Be("warehouse");
    }

    [Fact]
    public void OwnedDomains_ShouldContainWarehouse()
    {
        var agent = CreateAgent();
        agent.OwnedDomains.Should().Contain("warehouse");
    }

    [Fact]
    public void CanHandle_ShouldReturnTrueForWarehouseDomain()
    {
        var agent = CreateAgent();
        agent.CanHandle(new AgentTask { DomainHint = "warehouse" }).Should().BeTrue();
    }

    [Fact]
    public void CanHandle_ShouldReturnFalseForAccountingDomain()
    {
        var agent = CreateAgent();
        agent.CanHandle(new AgentTask { DomainHint = "accounting" }).Should().BeFalse();
    }

    // ────────────────────── Capability Resolution ──────────────────────

    [Theory]
    [InlineData("show me inventory summary", "warehouse.inventory.summary")]
    [InlineData("warehouse.inventory.by-item", "warehouse.inventory.by-item")]
    [InlineData("warehouse.receipts.recent", "warehouse.receipts.recent")]
    [InlineData("show inventory summary report", "warehouse.inventory.summary")]
    public void ResolveCapability_ShouldMatchKnownCapabilities(string input, string expectedKey)
    {
        var agent = CreateAgent();
        var cap = agent.ResolveCapability(input);

        cap.Should().NotBeNull();
        cap!.CapabilityKey.Should().Be(expectedKey);
    }

    [Theory]
    [InlineData("hello how are you")]
    [InlineData("what is the weather")]
    [InlineData("")]
    public void ResolveCapability_ShouldReturnNullForUnmatchedInput(string input)
    {
        var agent = CreateAgent();
        agent.ResolveCapability(input).Should().BeNull();
    }

    // ────────────────────── Native Execution Path ──────────────────────

    [Fact]
    public async Task ExecuteAsync_ShouldUseNativePath_WhenCapabilityMatches()
    {
        var mockAdapter = new Mock<IToolAdapter>();
        mockAdapter.Setup(a => a.ExecuteAsync(It.IsAny<ToolExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.Ok("{\"items\": 42}"));

        var mockAdapterRegistry = new Mock<IToolAdapterRegistry>();
        mockAdapterRegistry.Setup(r => r.Resolve("sql")).Returns(mockAdapter.Object);

        var agent = CreateAgent();
        var task = new AgentTask { Input = "show me inventory summary", DomainHint = "warehouse", IntentType = "query" };
        var context = CreateContext(mockAdapterRegistry.Object);

        var result = await agent.ExecuteAsync(task, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("42");

        mockAdapter.Verify(a => a.ExecuteAsync(
            It.Is<ToolExecutionRequest>(r =>
                r.CapabilityKey == "warehouse.inventory.summary" &&
                r.Operation == "execute_query" &&
                r.AgentId == "warehouse" &&
                r.SystemId == "sql"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenAdapterFails()
    {
        var mockAdapter = new Mock<IToolAdapter>();
        mockAdapter.Setup(a => a.ExecuteAsync(It.IsAny<ToolExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.Fail("SQL_ERROR", new { message = "Connection failed" }));

        var mockAdapterRegistry = new Mock<IToolAdapterRegistry>();
        mockAdapterRegistry.Setup(r => r.Resolve("sql")).Returns(mockAdapter.Object);

        var agent = CreateAgent();
        var task = new AgentTask { Input = "show me inventory summary", DomainHint = "warehouse" };
        var context = CreateContext(mockAdapterRegistry.Object);

        var result = await agent.ExecuteAsync(task, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be("SQL_ERROR");
    }

    [Fact]
    public async Task ExecuteAsync_NativePath_ShouldPassTenantIdToAdapter()
    {
        var mockAdapter = new Mock<IToolAdapter>();
        mockAdapter.Setup(a => a.ExecuteAsync(It.IsAny<ToolExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.Ok("{}"));

        var mockAdapterRegistry = new Mock<IToolAdapterRegistry>();
        mockAdapterRegistry.Setup(r => r.Resolve("sql")).Returns(mockAdapter.Object);

        var agent = CreateAgent();
        var task = new AgentTask { Input = "warehouse.receipts.recent", DomainHint = "warehouse" };
        var context = CreateContext(mockAdapterRegistry.Object);

        await agent.ExecuteAsync(task, context, CancellationToken.None);

        mockAdapter.Verify(a => a.ExecuteAsync(
            It.Is<ToolExecutionRequest>(r => r.TenantId == "tenant-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NativePath_ShouldIncludeStoredProcedureInMetadata()
    {
        var mockAdapter = new Mock<IToolAdapter>();
        ToolExecutionRequest? capturedRequest = null;
        mockAdapter.Setup(a => a.ExecuteAsync(It.IsAny<ToolExecutionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ToolExecutionRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(ToolExecutionResult.Ok("{}"));

        var mockAdapterRegistry = new Mock<IToolAdapterRegistry>();
        mockAdapterRegistry.Setup(r => r.Resolve("sql")).Returns(mockAdapter.Object);

        var agent = CreateAgent();
        var task = new AgentTask { Input = "show me inventory summary", DomainHint = "warehouse" };
        var context = CreateContext(mockAdapterRegistry.Object);

        await agent.ExecuteAsync(task, context, CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Metadata.Should().ContainKey("storedProcedure");
        capturedRequest.Metadata["storedProcedure"].Should().Be("dbo.ai_warehouse_inventory_summary");
    }

    // ────────────────────── Fallback Path ──────────────────────

    [Fact]
    public async Task ExecuteAsync_ShouldAttemptBridgeFallback_WhenNoCapabilityMatches()
    {
        // When no capability matches, the agent falls back to Bridge.ExecuteAsync.
        // Since our bridge is uninitialized (no pipeline), this will throw NullReferenceException,
        // proving the fallback path was actually taken.
        var agent = CreateAgent();
        var task = new AgentTask { Input = "hello how are you", DomainHint = "warehouse" };
        var context = CreateContext(new Mock<IToolAdapterRegistry>().Object);

        var act = () => agent.ExecuteAsync(task, context, CancellationToken.None);
        await act.Should().ThrowAsync<NullReferenceException>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFallback_WhenAdapterRegistryIsNull()
    {
        // Even when a capability matches, if ToolAdapterRegistry is null, fallback to bridge
        var agent = CreateAgent();
        var task = new AgentTask { Input = "show me inventory summary", DomainHint = "warehouse" };
        var context = CreateContext(null); // no adapter registry

        // Bridge will throw because it's uninitialized — proves fallback path was taken
        var act = () => agent.ExecuteAsync(task, context, CancellationToken.None);
        await act.Should().ThrowAsync<NullReferenceException>();
    }
}
