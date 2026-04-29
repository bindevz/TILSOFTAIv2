using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;
using Xunit;

namespace TILSOFTAI.Tests.AiRouting;

public sealed class OfficialAgentProviderFactoryTests
{
    [Fact]
    public void CreateAgent_WithOfficialOpenAiProvider_ReturnsMicrosoftAgentFrameworkAgent()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var factory = new OfficialAgentProviderFactory(
            [new Mock<IChatClient>().Object],
            Options.Create(new AiRoutingOptions
            {
                UseOfficialMicrosoftAgentFramework = true,
                Provider = OfficialAgentProviderFactory.OpenAiProvider,
                Model = "gpt-4o-mini"
            }),
            Options.Create(new LlmOptions()),
            NullLoggerFactory.Instance,
            services,
            NullLogger<OfficialAgentProviderFactory>.Instance);

        var agent = factory.CreateAgent(
            "You are a concise ERP assistant.",
            Array.Empty<AIFunction>(),
            new TilsoftExecutionContext
            {
                TenantId = "tenant-33",
                UserId = "user-33",
                CorrelationId = "corr-33"
            });

        agent.Should().BeAssignableTo<AIAgent>();
        agent.GetType().FullName.Should().Be("Microsoft.Agents.AI.ChatClientAgent");
        agent.GetType().FullName.Should().NotContain("CandidateGatedToolCallingAgent");
    }

    [Fact]
    public void CreateAgent_WithOpenAiCompatibleProvider_RejectsNonOfficialProvider()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var factory = new OfficialAgentProviderFactory(
            [new Mock<IChatClient>().Object],
            Options.Create(new AiRoutingOptions
            {
                UseOfficialMicrosoftAgentFramework = true,
                Provider = "OpenAiCompatible",
                Model = "gemma4:26b"
            }),
            Options.Create(new LlmOptions()),
            NullLoggerFactory.Instance,
            services,
            NullLogger<OfficialAgentProviderFactory>.Instance);

        var act = () => factory.CreateAgent(
            "You are a concise ERP assistant.",
            Array.Empty<AIFunction>(),
            new TilsoftExecutionContext());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AzureOpenAI, OpenAI, OpenAiCompatibleLocal, or OllamaOfficialProviderForDevOnly*");
    }
}
