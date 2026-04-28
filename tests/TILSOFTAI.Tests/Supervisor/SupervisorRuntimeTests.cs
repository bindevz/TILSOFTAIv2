using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TILSOFTAI.Agents.Abstractions;
using TILSOFTAI.Approvals;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.AiRouting;
using TILSOFTAI.Supervisor;
using TILSOFTAI.Supervisor.Classification;
using TILSOFTAI.Tools.Abstractions;
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

        var classifier = new Mock<IIntentClassifier>();
        classifier.Setup(x => x.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentClassification.Unclassified("test"));

        var approvalEngine = new Mock<IApprovalEngine>();
        var adapterRegistry = new Mock<IToolAdapterRegistry>();
        var logger = new Mock<ILogger<SupervisorRuntime>>();
        var runtime = new SupervisorRuntime(classifier.Object, registry.Object, approvalEngine.Object, adapterRegistry.Object, logger.Object);

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

        var classifier = new Mock<IIntentClassifier>();
        classifier.Setup(x => x.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IntentClassification.Unclassified("test"));

        var approvalEngine = new Mock<IApprovalEngine>();
        var adapterRegistry = new Mock<IToolAdapterRegistry>();
        var logger = new Mock<ILogger<SupervisorRuntime>>();
        var runtime = new SupervisorRuntime(classifier.Object, registry.Object, approvalEngine.Object, adapterRegistry.Object, logger.Object);

        var result = await runtime.RunAsync(
            new SupervisorRequest { Input = "show receivables" },
            new TilsoftExecutionContext(),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be("SUPERVISOR_AGENT_NOT_FOUND");
    }

    [Fact]
    public async Task RunAsync_ShouldClassifyIntentWhenNoDomainHintProvided()
    {
        var agent = new Mock<IDomainAgent>();
        agent.SetupGet(x => x.AgentId).Returns("warehouse");
        agent.SetupGet(x => x.DisplayName).Returns("Warehouse");
        agent.SetupGet(x => x.OwnedDomains).Returns(new[] { "warehouse" });
        agent.Setup(x => x.ExecuteAsync(
                It.IsAny<AgentTask>(),
                It.IsAny<AgentExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResult.Ok("warehouse handled"));

        var registry = new Mock<IAgentRegistry>();
        registry.Setup(x => x.ResolveCandidates(It.IsAny<AgentTask>()))
            .Returns(new[] { agent.Object });

        var classifier = new Mock<IIntentClassifier>();
        classifier.Setup(x => x.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntentClassification
            {
                DomainHint = "warehouse",
                Confidence = 0.8m,
                IntentType = "query",
                Reasons = new[] { "Matched keywords: [inventory]" }
            });

        var approvalEngine = new Mock<IApprovalEngine>();
        var adapterRegistry = new Mock<IToolAdapterRegistry>();
        var logger = new Mock<ILogger<SupervisorRuntime>>();
        var runtime = new SupervisorRuntime(classifier.Object, registry.Object, approvalEngine.Object, adapterRegistry.Object, logger.Object);

        var result = await runtime.RunAsync(
            new SupervisorRequest { Input = "check inventory levels" },
            new TilsoftExecutionContext { TenantId = "tenant-a", UserId = "user-a" },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("warehouse handled");

        classifier.Verify(
            x => x.ClassifyAsync("check inventory levels", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_ShouldUseLegacyPipelineWhenAgentRoutingFlagIsOff()
    {
        var agent = new Mock<IDomainAgent>();
        agent.SetupGet(x => x.AgentId).Returns("accounting");
        agent.SetupGet(x => x.DisplayName).Returns("Accounting");
        agent.SetupGet(x => x.OwnedDomains).Returns(new[] { "accounting" });
        agent.Setup(x => x.ExecuteAsync(
                It.IsAny<AgentTask>(),
                It.IsAny<AgentExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResult.Ok("legacy handled"));

        var registry = new Mock<IAgentRegistry>();
        registry.Setup(x => x.ResolveCandidates(It.IsAny<AgentTask>()))
            .Returns(new[] { agent.Object });

        var classifier = new Mock<IIntentClassifier>();
        var approvalEngine = new Mock<IApprovalEngine>();
        var adapterRegistry = new Mock<IToolAdapterRegistry>();
        var logger = new Mock<ILogger<SupervisorRuntime>>();
        var router = new Mock<IAgentToolRouter>();

        var runtime = new SupervisorRuntime(
            classifier.Object,
            registry.Object,
            approvalEngine.Object,
            adapterRegistry.Object,
            logger.Object,
            agentToolRouter: router.Object,
            aiRoutingOptions: Options.Create(new AiRoutingOptions
            {
                MicrosoftAgentFrameworkRoutingEnabled = false
            }));

        var result = await runtime.RunAsync(
            new SupervisorRequest
            {
                Input = "show receivables",
                DomainHint = "accounting"
            },
            new TilsoftExecutionContext(),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("legacy handled");
        router.Verify(
            x => x.TryRouteAsync(It.IsAny<AgentToolRoutingRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_ShouldFallbackToLegacyPipelineWhenAgentRoutingDoesNotHandle()
    {
        var agent = new Mock<IDomainAgent>();
        agent.SetupGet(x => x.AgentId).Returns("warehouse");
        agent.SetupGet(x => x.DisplayName).Returns("Warehouse");
        agent.SetupGet(x => x.OwnedDomains).Returns(new[] { "warehouse" });
        agent.Setup(x => x.ExecuteAsync(
                It.IsAny<AgentTask>(),
                It.IsAny<AgentExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResult.Ok("fallback handled"));

        var registry = new Mock<IAgentRegistry>();
        registry.Setup(x => x.ResolveCandidates(It.IsAny<AgentTask>()))
            .Returns(new[] { agent.Object });

        var classifier = new Mock<IIntentClassifier>();
        var approvalEngine = new Mock<IApprovalEngine>();
        var adapterRegistry = new Mock<IToolAdapterRegistry>();
        var logger = new Mock<ILogger<SupervisorRuntime>>();
        var router = new Mock<IAgentToolRouter>();
        router.Setup(x => x.TryRouteAsync(It.IsAny<AgentToolRoutingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentToolRoutingResult
            {
                Handled = false,
                FailureReason = "stub"
            });

        var runtime = new SupervisorRuntime(
            classifier.Object,
            registry.Object,
            approvalEngine.Object,
            adapterRegistry.Object,
            logger.Object,
            agentToolRouter: router.Object,
            aiRoutingOptions: Options.Create(new AiRoutingOptions
            {
                MicrosoftAgentFrameworkRoutingEnabled = true,
                FallbackToLegacyPipeline = true
            }));

        var result = await runtime.RunAsync(
            new SupervisorRequest
            {
                Input = "check inventory levels",
                DomainHint = "warehouse"
            },
            new TilsoftExecutionContext { Language = "en" },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("fallback handled");
        router.Verify(
            x => x.TryRouteAsync(
                It.Is<AgentToolRoutingRequest>(r =>
                    r.Message == "check inventory levels"
                    && r.Locale == "en"
                    && r.RequestedAnswerMode == AnswerMode.Structured),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_ShouldFailWhenAgentRoutingDoesNotHandleAndFallbackIsDisabled()
    {
        var registry = new Mock<IAgentRegistry>();
        var classifier = new Mock<IIntentClassifier>();
        var approvalEngine = new Mock<IApprovalEngine>();
        var adapterRegistry = new Mock<IToolAdapterRegistry>();
        var logger = new Mock<ILogger<SupervisorRuntime>>();
        var router = new Mock<IAgentToolRouter>();
        router.Setup(x => x.TryRouteAsync(It.IsAny<AgentToolRoutingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentToolRoutingResult
            {
                Handled = false,
                FailureReason = "stub failed"
            });

        var runtime = new SupervisorRuntime(
            classifier.Object,
            registry.Object,
            approvalEngine.Object,
            adapterRegistry.Object,
            logger.Object,
            agentToolRouter: router.Object,
            aiRoutingOptions: Options.Create(new AiRoutingOptions
            {
                MicrosoftAgentFrameworkRoutingEnabled = true,
                FallbackToLegacyPipeline = false
            }));

        var result = await runtime.RunAsync(
            new SupervisorRequest { Input = "show receivables" },
            new TilsoftExecutionContext(),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be("AGENT_ROUTING_FAILED");
        result.Error.Should().Be("stub failed");
        registry.Verify(x => x.ResolveCandidates(It.IsAny<AgentTask>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_ShouldFailClosedWhenOfficialRoutingDoesNotHandle()
    {
        var registry = new Mock<IAgentRegistry>();
        var classifier = new Mock<IIntentClassifier>();
        var approvalEngine = new Mock<IApprovalEngine>();
        var adapterRegistry = new Mock<IToolAdapterRegistry>();
        var logger = new Mock<ILogger<SupervisorRuntime>>();
        var router = new Mock<IAgentToolRouter>();
        router.Setup(x => x.TryRouteAsync(It.IsAny<AgentToolRoutingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentToolRoutingResult
            {
                Handled = false,
                FailureReason = "official route failed"
            });

        var runtime = new SupervisorRuntime(
            classifier.Object,
            registry.Object,
            approvalEngine.Object,
            adapterRegistry.Object,
            logger.Object,
            agentToolRouter: router.Object,
            aiRoutingOptions: Options.Create(new AiRoutingOptions
            {
                UseOfficialMicrosoftAgentFramework = true,
                FallbackToLegacyPipeline = true
            }));

        var result = await runtime.RunAsync(
            new SupervisorRequest { Input = "show receivables" },
            new TilsoftExecutionContext(),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be("AGENT_ROUTING_FAILED");
        result.Error.Should().Be("official route failed");
        registry.Verify(x => x.ResolveCandidates(It.IsAny<AgentTask>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenAgentRoutingTenantGateDoesNotMatch_ShouldUseLegacyPipeline()
    {
        var agent = new Mock<IDomainAgent>();
        agent.SetupGet(x => x.AgentId).Returns("warehouse");
        agent.SetupGet(x => x.DisplayName).Returns("Warehouse");
        agent.SetupGet(x => x.OwnedDomains).Returns(new[] { "warehouse" });
        agent.Setup(x => x.ExecuteAsync(
                It.IsAny<AgentTask>(),
                It.IsAny<AgentExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResult.Ok("legacy rollout handled"));

        var registry = new Mock<IAgentRegistry>();
        registry.Setup(x => x.ResolveCandidates(It.IsAny<AgentTask>()))
            .Returns(new[] { agent.Object });

        var classifier = new Mock<IIntentClassifier>();
        var approvalEngine = new Mock<IApprovalEngine>();
        var adapterRegistry = new Mock<IToolAdapterRegistry>();
        var logger = new Mock<ILogger<SupervisorRuntime>>();
        var router = new Mock<IAgentToolRouter>();

        var runtime = new SupervisorRuntime(
            classifier.Object,
            registry.Object,
            approvalEngine.Object,
            adapterRegistry.Object,
            logger.Object,
            agentToolRouter: router.Object,
            aiRoutingOptions: Options.Create(new AiRoutingOptions
            {
                MicrosoftAgentFrameworkRoutingEnabled = true,
                EnabledTenantIds = ["tenant-enabled"]
            }));

        var result = await runtime.RunAsync(
            new SupervisorRequest
            {
                Input = "check inventory levels",
                DomainHint = "warehouse"
            },
            new TilsoftExecutionContext { TenantId = "tenant-disabled", UserId = "user-a" },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("legacy rollout handled");
        router.Verify(
            x => x.TryRouteAsync(It.IsAny<AgentToolRoutingRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenOfficialRoutingTenantGateDoesNotMatch_ShouldFailClosed()
    {
        var registry = new Mock<IAgentRegistry>();
        var classifier = new Mock<IIntentClassifier>();
        var approvalEngine = new Mock<IApprovalEngine>();
        var adapterRegistry = new Mock<IToolAdapterRegistry>();
        var logger = new Mock<ILogger<SupervisorRuntime>>();
        var router = new Mock<IAgentToolRouter>();

        var runtime = new SupervisorRuntime(
            classifier.Object,
            registry.Object,
            approvalEngine.Object,
            adapterRegistry.Object,
            logger.Object,
            agentToolRouter: router.Object,
            aiRoutingOptions: Options.Create(new AiRoutingOptions
            {
                UseOfficialMicrosoftAgentFramework = true,
                EnabledTenantIds = ["tenant-enabled"]
            }));

        var result = await runtime.RunAsync(
            new SupervisorRequest
            {
                Input = "check inventory levels",
                DomainHint = "warehouse"
            },
            new TilsoftExecutionContext { TenantId = "tenant-disabled", UserId = "user-a" },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be("AGENT_ROUTING_ROLLOUT_BLOCKED");
        router.Verify(
            x => x.TryRouteAsync(It.IsAny<AgentToolRoutingRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        registry.Verify(x => x.ResolveCandidates(It.IsAny<AgentTask>()), Times.Never);
    }
}
