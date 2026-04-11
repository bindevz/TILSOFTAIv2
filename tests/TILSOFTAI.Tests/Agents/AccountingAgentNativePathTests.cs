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

public sealed class AccountingAgentNativePathTests
{
    private static LegacyChatPipelineBridge CreateUninitializedBridge() =>
        (LegacyChatPipelineBridge)RuntimeHelpers.GetUninitializedObject(typeof(LegacyChatPipelineBridge));

    private static AccountingAgent CreateAgent(ICapabilityRegistry? capabilityRegistry = null)
    {
        var capReg = capabilityRegistry ?? new InMemoryCapabilityRegistry(AccountingCapabilities.All);
        var resolver = new StructuredCapabilityResolver(new Mock<ILogger<StructuredCapabilityResolver>>().Object);
        var logger = new Mock<ILogger<AccountingAgent>>().Object;
        return new AccountingAgent(CreateUninitializedBridge(), capReg, resolver, logger);
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

    // ────────────────────── Identity ──────────────────────────

    [Fact]
    public void AgentId_ShouldBeAccounting()
    {
        var agent = CreateAgent();
        agent.AgentId.Should().Be("accounting");
    }

    [Fact]
    public void OwnedDomains_ShouldContainAccounting()
    {
        var agent = CreateAgent();
        agent.OwnedDomains.Should().Contain("accounting");
    }

    [Fact]
    public void CanHandle_ShouldReturnTrueForAccountingDomain()
    {
        var agent = CreateAgent();
        agent.CanHandle(new AgentTask { DomainHint = "accounting" }).Should().BeTrue();
    }

    [Fact]
    public void CanHandle_ShouldReturnFalseForWarehouseDomain()
    {
        var agent = CreateAgent();
        agent.CanHandle(new AgentTask { DomainHint = "warehouse" }).Should().BeFalse();
    }

    // ────────────────── Capability Resolution via Hint ────────────────────

    [Fact]
    public void ResolveCapability_WithExactKeyHint_ShouldMatch()
    {
        var agent = CreateAgent();
        var task = new AgentTask
        {
            Input = "anything",
            CapabilityHint = new CapabilityRequestHint
            {
                CapabilityKey = "accounting.receivables.summary",
                Domain = "accounting"
            }
        };

        var cap = agent.ResolveCapability(task, AccountingCapabilities.All);
        cap.Should().NotBeNull();
        cap!.CapabilityKey.Should().Be("accounting.receivables.summary");
    }

    [Fact]
    public void ResolveCapability_WithKeywordHint_ShouldMatch()
    {
        var agent = CreateAgent();
        var task = new AgentTask
        {
            Input = "show me payables summary",
            CapabilityHint = new CapabilityRequestHint
            {
                Domain = "accounting",
                SubjectKeywords = new[] { "payables", "summary" }
            }
        };

        var cap = agent.ResolveCapability(task, AccountingCapabilities.All);
        cap.Should().NotBeNull();
        cap!.CapabilityKey.Should().Be("accounting.payables.summary");
    }

    [Fact]
    public void ResolveCapability_WithNoMatchHint_ShouldReturnNull()
    {
        var agent = CreateAgent();
        var task = new AgentTask
        {
            Input = "hello how are you",
            CapabilityHint = new CapabilityRequestHint
            {
                Domain = "accounting",
                SubjectKeywords = new[] { "hello", "how" }
            }
        };

        var cap = agent.ResolveCapability(task, AccountingCapabilities.All);
        cap.Should().BeNull();
    }

    // ────────────────────── Native Execution Path ──────────────────────

    [Fact]
    public async Task ExecuteAsync_ShouldUseNativePath_WhenCapabilityMatches()
    {
        var mockAdapter = new Mock<IToolAdapter>();
        mockAdapter.Setup(a => a.ExecuteAsync(It.IsAny<ToolExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolExecutionResult.Ok("{\"total\": 125000}"));

        var mockAdapterRegistry = new Mock<IToolAdapterRegistry>();
        mockAdapterRegistry.Setup(r => r.Resolve("sql")).Returns(mockAdapter.Object);

        var agent = CreateAgent();
        var task = new AgentTask
        {
            Input = "show me receivables summary",
            DomainHint = "accounting",
            IntentType = "query",
            CapabilityHint = new CapabilityRequestHint
            {
                Domain = "accounting",
                SubjectKeywords = new[] { "receivables", "summary" }
            }
        };
        var context = CreateContext(mockAdapterRegistry.Object);

        var result = await agent.ExecuteAsync(task, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("125000");

        mockAdapter.Verify(a => a.ExecuteAsync(
            It.Is<ToolExecutionRequest>(r =>
                r.CapabilityKey == "accounting.receivables.summary" &&
                r.Operation == "execute_query" &&
                r.AgentId == "accounting" &&
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
        var task = new AgentTask
        {
            Input = "show me payables summary",
            DomainHint = "accounting",
            CapabilityHint = new CapabilityRequestHint
            {
                Domain = "accounting",
                SubjectKeywords = new[] { "payables", "summary" }
            }
        };
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
        var task = new AgentTask
        {
            Input = "invoice by number",
            DomainHint = "accounting",
            CapabilityHint = new CapabilityRequestHint
            {
                Domain = "accounting",
                SubjectKeywords = new[] { "invoice", "number" }
            }
        };
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
        var task = new AgentTask
        {
            Input = "receivables summary",
            DomainHint = "accounting",
            CapabilityHint = new CapabilityRequestHint
            {
                Domain = "accounting",
                SubjectKeywords = new[] { "receivables", "summary" }
            }
        };
        var context = CreateContext(mockAdapterRegistry.Object);

        await agent.ExecuteAsync(task, context, CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Metadata.Should().ContainKey("storedProcedure");
        capturedRequest.Metadata["storedProcedure"].Should().Be("dbo.ai_accounting_receivables_summary");
    }

    // ────────────────────── Fallback Path ──────────────────────

    [Fact]
    public async Task ExecuteAsync_ShouldAttemptBridgeFallback_WhenNoCapabilityMatches()
    {
        var agent = CreateAgent();
        var task = new AgentTask
        {
            Input = "hello how are you",
            DomainHint = "accounting",
            CapabilityHint = new CapabilityRequestHint
            {
                Domain = "accounting",
                SubjectKeywords = new[] { "hello" }
            }
        };
        var context = CreateContext(new Mock<IToolAdapterRegistry>().Object);

        var act = () => agent.ExecuteAsync(task, context, CancellationToken.None);
        await act.Should().ThrowAsync<NullReferenceException>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFallback_WhenAdapterRegistryIsNull()
    {
        var agent = CreateAgent();
        var task = new AgentTask
        {
            Input = "receivables summary",
            DomainHint = "accounting",
            CapabilityHint = new CapabilityRequestHint
            {
                Domain = "accounting",
                SubjectKeywords = new[] { "receivables", "summary" }
            }
        };
        var context = CreateContext(null);

        var act = () => agent.ExecuteAsync(task, context, CancellationToken.None);
        await act.Should().ThrowAsync<NullReferenceException>();
    }
}
