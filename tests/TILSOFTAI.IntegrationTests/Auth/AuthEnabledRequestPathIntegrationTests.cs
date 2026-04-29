using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.AiRouting;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Supervisor;
using Xunit;

namespace TILSOFTAI.IntegrationTests.Auth;

/// <summary>
/// Validates that authenticated request context is threaded into the official Agent Framework router.
/// </summary>
public sealed class AuthEnabledRequestPathIntegrationTests
{
    [Fact]
    public async Task AuthenticatedRequest_ShouldThreadTenantAndCorrelationToOfficialRouter()
    {
        var router = new Mock<IAgentToolRouter>();
        router.Setup(x => x.TryRouteAsync(It.IsAny<AgentToolRoutingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentToolRoutingResult
            {
                Handled = true,
                Answer = Answer("ok")
            });
        var runtime = BuildRuntime(router.Object);

        var ctx = new TilsoftExecutionContext
        {
            TenantId = "tenant-auth-123",
            UserId = "user-auth-456",
            Roles = new[] { "ai_user", "model_read" },
            CorrelationId = "corr-auth-789"
        };

        var result = await runtime.RunAsync(
            new SupervisorRequest { Input = "show model summary" },
            ctx,
            CancellationToken.None);

        result.Success.Should().BeTrue();
        router.Verify(
            x => x.TryRouteAsync(
                It.Is<AgentToolRoutingRequest>(r =>
                    r.ExecutionContext.TenantId == "tenant-auth-123"
                    && r.ExecutionContext.UserId == "user-auth-456"
                    && r.ExecutionContext.CorrelationId == "corr-auth-789"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AuthenticatedRequest_WithEmptyTenantId_ShouldStillReachOfficialRouter()
    {
        var router = new Mock<IAgentToolRouter>();
        router.Setup(x => x.TryRouteAsync(It.IsAny<AgentToolRoutingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentToolRoutingResult
            {
                Handled = true,
                Answer = Answer("ok")
            });
        var runtime = BuildRuntime(router.Object);

        var ctx = new TilsoftExecutionContext
        {
            TenantId = "",
            UserId = "user-empty",
            CorrelationId = "corr-empty",
            Roles = new[] { "model_read" }
        };

        var result = await runtime.RunAsync(
            new SupervisorRequest { Input = "show model summary" },
            ctx,
            CancellationToken.None);

        result.Success.Should().BeTrue();
        router.Verify(
            x => x.TryRouteAsync(
                It.Is<AgentToolRoutingRequest>(r => r.ExecutionContext.TenantId == ""),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static SupervisorRuntime BuildRuntime(IAgentToolRouter router) =>
        new(
            Mock.Of<ILogger<SupervisorRuntime>>(),
            router,
            Options.Create(new AiRoutingOptions()));

    private static AssistantAnswer Answer(string text) => new()
    {
        AnswerType = "structured",
        Text = text,
        Blocks = Array.Empty<AnswerBlock>(),
        Provenance = new AnswerProvenance
        {
            CapabilityKey = "model.test",
            RowCount = 1
        },
        SelectedAgentId = "microsoft-agent-router"
    };
}
