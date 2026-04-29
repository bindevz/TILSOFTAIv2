using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Actions;
using TILSOFTAI.Orchestration.AiRouting;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Supervisor;
using Xunit;

namespace TILSOFTAI.Tests.Supervisor;

public sealed class SupervisorRuntimeTests
{
    [Fact]
    public async Task RunAsync_ShouldRouteWithOfficialAgentRouter()
    {
        var router = new Mock<IAgentToolRouter>();
        router.Setup(x => x.TryRouteAsync(It.IsAny<AgentToolRoutingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentToolRoutingResult
            {
                Handled = true,
                Answer = Answer("handled", selectedAgentId: "microsoft-agent-router")
            });

        var runtime = CreateRuntime(router.Object);

        var result = await runtime.RunAsync(
            new SupervisorRequest
            {
                Input = "show model options",
                Metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["answerMode"] = "rawJson"
                }
            },
            new TilsoftExecutionContext
            {
                TenantId = "tenant-a",
                UserId = "user-a",
                Language = "en-US"
            },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("handled");
        result.SelectedAgentId.Should().Be("microsoft-agent-router");
        router.Verify(
            x => x.TryRouteAsync(
                It.Is<AgentToolRoutingRequest>(r =>
                    r.Message == "show model options"
                    && r.Locale == "en-US"
                    && r.RequestedAnswerMode == AnswerMode.RawJson),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_ShouldFailClosedWhenOfficialRoutingDoesNotHandle()
    {
        var router = new Mock<IAgentToolRouter>();
        router.Setup(x => x.TryRouteAsync(It.IsAny<AgentToolRoutingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentToolRoutingResult
            {
                Handled = false,
                FailureReason = "official route failed"
            });

        var runtime = CreateRuntime(router.Object);

        var result = await runtime.RunAsync(
            new SupervisorRequest { Input = "show model options" },
            new TilsoftExecutionContext { CorrelationId = "corr-1" },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be("AGENT_ROUTING_FAILED");
        result.Error.Should().Be("official route failed");
        result.SelectedAgentId.Should().Be("microsoft-agent-router");
        result.Detail.Should().BeEquivalentTo(new
        {
            correlationId = "corr-1",
            fallbackUsed = false
        });
    }

    [Fact]
    public async Task RunAsync_WhenTenantGateDoesNotMatch_ShouldFailClosedWithoutRouting()
    {
        var router = new Mock<IAgentToolRouter>();
        var runtime = CreateRuntime(
            router.Object,
            new AiRoutingOptions { EnabledTenantIds = ["tenant-enabled"] });

        var result = await runtime.RunAsync(
            new SupervisorRequest { Input = "show model options" },
            new TilsoftExecutionContext { TenantId = "tenant-disabled", UserId = "user-a" },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be("AGENT_ROUTING_ROLLOUT_BLOCKED");
        router.Verify(
            x => x.TryRouteAsync(It.IsAny<AgentToolRoutingRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_ShouldReturnPendingActionConfirmationAnswerBeforeRouting()
    {
        var router = new Mock<IAgentToolRouter>();
        var pending = new Mock<IPendingActionConfirmationResolver>();
        pending.Setup(x => x.TryResolveAsync("yes", It.IsAny<TilsoftExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PendingActionConfirmationResult(
                true,
                Answer("confirmation handled", selectedAgentId: "pending-action-confirmation")));

        var runtime = CreateRuntime(
            router.Object,
            pendingActionConfirmationResolver: pending.Object);

        var result = await runtime.RunAsync(
            new SupervisorRequest { Input = "yes" },
            new TilsoftExecutionContext(),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("confirmation handled");
        result.SelectedAgentId.Should().Be("pending-action-confirmation");
        router.Verify(
            x => x.TryRouteAsync(It.IsAny<AgentToolRoutingRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_ShouldRejectBlankInput()
    {
        var router = new Mock<IAgentToolRouter>();
        var runtime = CreateRuntime(router.Object);

        var result = await runtime.RunAsync(
            new SupervisorRequest { Input = " " },
            new TilsoftExecutionContext(),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Input is required.");
        router.Verify(
            x => x.TryRouteAsync(It.IsAny<AgentToolRoutingRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static SupervisorRuntime CreateRuntime(
        IAgentToolRouter router,
        AiRoutingOptions? options = null,
        IPendingActionConfirmationResolver? pendingActionConfirmationResolver = null) =>
        new(
            Mock.Of<ILogger<SupervisorRuntime>>(),
            router,
            Options.Create(options ?? new AiRoutingOptions()),
            pendingActionConfirmationResolver);

    private static AssistantAnswer Answer(string text, string? selectedAgentId = null) => new()
    {
        AnswerType = "structured",
        Text = text,
        Blocks = Array.Empty<AnswerBlock>(),
        Provenance = new AnswerProvenance
        {
            CapabilityKey = "model.test",
            RowCount = 1
        },
        SelectedAgentId = selectedAgentId
    };
}
