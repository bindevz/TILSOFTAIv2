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
/// Sprint 5: Integration tests for the Accounting native execution path.
/// Tests real wiring: SupervisorRuntime → classification → AccountingAgent → capability resolver → adapter.
/// Uses stub adapter to validate the full execution chain.
/// </summary>
public sealed class AccountingNativePathIntegrationTests
{
    /// <summary>
    /// Builds a fully-wired SupervisorRuntime with real components (not mocks),
    /// using a stub adapter that returns canned data.
    /// </summary>
    private static (SupervisorRuntime runtime, Mock<IToolAdapter> stubAdapter) BuildRuntime()
    {
        var stubAdapter = new Mock<IToolAdapter>();
        stubAdapter.Setup(a => a.AdapterType).Returns("sql");
        stubAdapter.Setup(a => a.ExecuteAsync(It.IsAny<ToolExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.Ok("{\"result\": \"accounting_data\"}"));

        var adapterRegistry = new ToolAdapterRegistry(new[] { stubAdapter.Object });

        var capabilityResolver = new StructuredCapabilityResolver(
            new Mock<ILogger<StructuredCapabilityResolver>>().Object);

        var capabilityRegistry = new InMemoryCapabilityRegistry(
            WarehouseCapabilities.All.Concat(AccountingCapabilities.All));

        var accountingAgent = new AccountingAgent(
            capabilityRegistry,
            capabilityResolver,
            new Mock<ILogger<AccountingAgent>>().Object);

        var warehouseAgent = new WarehouseAgent(
            capabilityRegistry,
            capabilityResolver,
            new Mock<ILogger<WarehouseAgent>>().Object);

        var agents = new IDomainAgent[] { accountingAgent, warehouseAgent };
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
    public async Task AccountingRequest_ShouldRouteToAccountingAgent_AndExecuteNatively()
    {
        var (runtime, stubAdapter) = BuildRuntime();

        var request = new SupervisorRequest
        {
            Input = "show me receivables summary for accounting"
        };

        var ctx = new TilsoftExecutionContext
        {
            TenantId = "tenant-test",
            UserId = "user-test",
            CorrelationId = "corr-test",
            Roles = new[] { "accounting_read" }
        };

        var result = await runtime.RunAsync(request, ctx, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SelectedAgentId.Should().Be("accounting");
        result.Output.Should().Contain("accounting_data");

        // Verify the stub adapter was actually called with accounting capability
        stubAdapter.Verify(a => a.ExecuteAsync(
            It.Is<ToolExecutionRequest>(r =>
                r.CapabilityKey.StartsWith("accounting.") &&
                r.AgentId == "accounting" &&
                r.TenantId == "tenant-test"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AccountingRequest_WithExplicitCapabilityKey_ShouldResolveExactly()
    {
        var (runtime, stubAdapter) = BuildRuntime();

        var metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["capabilityKey"] = "accounting.payables.summary"
        };

        var request = new SupervisorRequest
        {
            Input = "accounting payables information",
            Metadata = metadata
        };

        var ctx = new TilsoftExecutionContext
        {
            TenantId = "tenant-explicit",
            CorrelationId = "corr-explicit",
            Roles = new[] { "accounting_read" }
        };

        var result = await runtime.RunAsync(request, ctx, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SelectedAgentId.Should().Be("accounting");

        stubAdapter.Verify(a => a.ExecuteAsync(
            It.Is<ToolExecutionRequest>(r =>
                r.CapabilityKey == "accounting.payables.summary" &&
                r.Metadata.ContainsKey("storedProcedure")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AccountingRequest_ShouldPassTenantIsolation()
    {
        var (runtime, stubAdapter) = BuildRuntime();

        var request = new SupervisorRequest
        {
            Input = "show invoice by number for accounting",
            Metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["arguments"] = "{\"@InvoiceNumber\":\"INV-001\"}"
            }
        };

        var ctx = new TilsoftExecutionContext
        {
            TenantId = "tenant-isolated",
            UserId = "user-isolated",
            CorrelationId = "corr-isolated",
            Roles = new[] { "accounting_read" }
        };

        var result = await runtime.RunAsync(request, ctx, CancellationToken.None);

        result.Success.Should().BeTrue();

        stubAdapter.Verify(a => a.ExecuteAsync(
            It.Is<ToolExecutionRequest>(r =>
                r.TenantId == "tenant-isolated" &&
                r.CorrelationId == "corr-isolated"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
