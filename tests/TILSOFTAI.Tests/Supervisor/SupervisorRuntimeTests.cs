using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TILSOFTAI.Agents.Abstractions;
using TILSOFTAI.Approvals;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Supervisor;
using Xunit;

namespace TILSOFTAI.Tests.Supervisor;

public sealed class SupervisorRuntimeTests
{
    [Fact]
    public async Task RunAsync_ShouldRouteToResolvedAgent()
    {
        var agent = new Mock<IDomainAgent>();
        agent.SetupGet(x => x.AgentId).Returns("accounting");
        agent.SetupGet(x => x.DisplayName).Returns("Accounting");
        agent.SetupGet(x => x.OwnedDomains).Returns(new[] { "accounting" });
        agent.Setup(x => x.ExecuteAsync(
                It.IsAny<AgentTask>(),
                It.IsAny<AgentExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResult.Ok("handled"));

        var registry = new Mock<IAgentRegistry>();
        registry.Setup(x => x.ResolveCandidates(It.IsAny<AgentTask>()))
            .Returns(new[] { agent.Object });

        var approvalEngine = new Mock<IApprovalEngine>();
        var logger = new Mock<ILogger<SupervisorRuntime>>();
        var runtime = new SupervisorRuntime(registry.Object, approvalEngine.Object, logger.Object);

        var result = await runtime.RunAsync(
            new SupervisorRequest
            {
                Input = "show receivables",
                DomainHint = "accounting"
            },
            new TilsoftExecutionContext { TenantId = "tenant-a", UserId = "user-a" },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("handled");
        result.SelectedAgentId.Should().Be("accounting");
    }

    [Fact]
    public async Task RunAsync_ShouldFailWhenNoAgentCanHandleRequest()
    {
        var registry = new Mock<IAgentRegistry>();
        registry.Setup(x => x.ResolveCandidates(It.IsAny<AgentTask>()))
            .Returns(Array.Empty<IDomainAgent>());

        var approvalEngine = new Mock<IApprovalEngine>();
        var logger = new Mock<ILogger<SupervisorRuntime>>();
        var runtime = new SupervisorRuntime(registry.Object, approvalEngine.Object, logger.Object);

        var result = await runtime.RunAsync(
            new SupervisorRequest { Input = "show receivables" },
            new TilsoftExecutionContext(),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be("SUPERVISOR_AGENT_NOT_FOUND");
    }
}
