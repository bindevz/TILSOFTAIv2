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

namespace TILSOFTAI.IntegrationTests.Auth;

/// <summary>
/// Sprint 5: Integration tests for auth-enabled request paths.
/// Validates that TilsoftExecutionContext (tenant, user, roles, correlation)
/// is properly threaded through the full supervisor → agent → adapter chain.
/// </summary>
public sealed class AuthEnabledRequestPathIntegrationTests
{
    private static LegacyChatPipelineBridge CreateUninitializedBridge() =>
        (LegacyChatPipelineBridge)RuntimeHelpers.GetUninitializedObject(typeof(LegacyChatPipelineBridge));

    private static (SupervisorRuntime runtime, Mock<IToolAdapter> stubAdapter) BuildRuntime()
    {
        var stubAdapter = new Mock<IToolAdapter>();
        stubAdapter.Setup(a => a.AdapterType).Returns("sql");
        stubAdapter.Setup(a => a.ExecuteAsync(It.IsAny<ToolExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.Ok("{\"data\": \"ok\"}"));

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
    public async Task AuthenticatedRequest_ShouldThreadTenantId_ThroughEntireChain()
    {
        var (runtime, stubAdapter) = BuildRuntime();

        var ctx = new TilsoftExecutionContext
        {
            TenantId = "tenant-auth-123",
            UserId = "user-auth-456",
            Roles = new[] { "ai_user", "warehouse_read" },
            CorrelationId = "corr-auth-789"
        };

        var request = new SupervisorRequest
        {
            Input = "show me warehouse inventory summary"
        };

        var result = await runtime.RunAsync(request, ctx, CancellationToken.None);

        result.Success.Should().BeTrue();

        stubAdapter.Verify(a => a.ExecuteAsync(
            It.Is<ToolExecutionRequest>(r =>
                r.TenantId == "tenant-auth-123" &&
                r.CorrelationId == "corr-auth-789"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticatedRequest_ShouldThreadContext_ForAccountingAgent()
    {
        var (runtime, stubAdapter) = BuildRuntime();

        var ctx = new TilsoftExecutionContext
        {
            TenantId = "tenant-acct",
            UserId = "user-acct",
            Roles = new[] { "ai_user", "accounting_read" },
            CorrelationId = "corr-acct"
        };

        var request = new SupervisorRequest
        {
            Input = "show me accounting receivables summary"
        };

        var result = await runtime.RunAsync(request, ctx, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SelectedAgentId.Should().Be("accounting");

        stubAdapter.Verify(a => a.ExecuteAsync(
            It.Is<ToolExecutionRequest>(r =>
                r.TenantId == "tenant-acct" &&
                r.CorrelationId == "corr-acct" &&
                r.AgentId == "accounting"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticatedRequest_WithEmptyTenantId_ShouldStillExecute()
    {
        var (runtime, stubAdapter) = BuildRuntime();

        var ctx = new TilsoftExecutionContext
        {
            TenantId = "",
            UserId = "user-empty",
            CorrelationId = "corr-empty",
            Roles = new[] { "warehouse_read" }
        };

        var request = new SupervisorRequest
        {
            Input = "show me warehouse inventory summary"
        };

        var result = await runtime.RunAsync(request, ctx, CancellationToken.None);

        // Should still succeed — tenant isolation is the adapter's responsibility
        result.Success.Should().BeTrue();

        stubAdapter.Verify(a => a.ExecuteAsync(
            It.Is<ToolExecutionRequest>(r => r.TenantId == ""),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
