using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TILSOFTAI.Agents;
using TILSOFTAI.Agents.Abstractions;
using TILSOFTAI.Approvals;
using TILSOFTAI.Domain.ExecutionContext;
using Xunit;

namespace TILSOFTAI.Tests.Agents;

public sealed class GeneralChatAgentTests
{
    [Fact]
    public void CanHandle_ShouldClaimUnclassifiedRequests()
    {
        var agent = CreateAgent();

        agent.CanHandle(new AgentTask { Input = "hello" }).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNativeHelp_ForGeneralConversation()
    {
        var agent = CreateAgent();

        var result = await agent.ExecuteAsync(
            new AgentTask { Input = "hello", IntentType = "chat" },
            CreateContext(),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("warehouse");
        result.Output.Should().Contain("accounting");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnUnsupported_WhenUnmatchedRequestIsNotGeneral()
    {
        var agent = CreateAgent();

        var result = await agent.ExecuteAsync(
            new AgentTask { Input = "calculate something mysterious", DomainHint = "cross-domain" },
            CreateContext(),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be("GENERAL_REQUEST_UNSUPPORTED");
    }

    private static GeneralChatAgent CreateAgent() => new(
        new Mock<ILogger<GeneralChatAgent>>().Object);

    private static AgentExecutionContext CreateContext() => new()
    {
        RuntimeContext = new TilsoftExecutionContext
        {
            TenantId = "tenant-1",
            UserId = "user-1",
            CorrelationId = "corr-1"
        },
        ApprovalEngine = new Mock<IApprovalEngine>().Object
    };
}
