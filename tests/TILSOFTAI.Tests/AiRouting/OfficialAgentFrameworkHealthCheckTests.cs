using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;
using TILSOFTAI.Api.Health;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;
using Xunit;

namespace TILSOFTAI.Tests.AiRouting;

public sealed class OfficialAgentFrameworkHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenOfficialRouteEnabledAndProviderIsEmpty_ShouldBeUnhealthy()
    {
        var options = ValidAiRouting();
        options.Provider = string.Empty;
        var healthCheck = CreateHealthCheck(aiRouting: options);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("AiRouting:Provider");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenLocalAiModelIsEmpty_ShouldBeUnhealthy()
    {
        var options = ValidLocalAi();
        options.Model = string.Empty;
        var healthCheck = CreateHealthCheck(localAi: options);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("LocalAi:Model");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenLocalAiModelIsPlaceholder_ShouldBeUnhealthy()
    {
        var options = ValidLocalAi();
        options.Model = "CHANGE_ME_TOOL_CALLING_MODEL";
        var healthCheck = CreateHealthCheck(localAi: options);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Data["local_ai_model_placeholder"].Should().Be(true);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenOfficialRouteIncludesNonModelDomain_ShouldBeUnhealthy()
    {
        var options = ValidAiRouting();
        options.AllowedDomains = ["model", "sales"];
        var healthCheck = CreateHealthCheck(aiRouting: options);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("AllowedDomains");
        result.Data["model_only_runtime"].Should().Be(false);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenOfficialRouteHasLegacyFallbackEnabled_ShouldBeUnhealthy()
    {
        var options = ValidAiRouting();
        options.FallbackToLegacyPipeline = true;
        var healthCheck = CreateHealthCheck(aiRouting: options);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("FallbackToLegacyPipeline");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenOfficialRouteHasNoChatClient_ShouldBeUnhealthy()
    {
        var healthCheck = CreateHealthCheck(chatClients: []);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("IChatClient");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenOfficialLocalRouteIsConfigured_ShouldBeHealthy()
    {
        var healthCheck = CreateHealthCheck();

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data["provider"].Should().Be(OfficialAgentProviderFactory.OpenAiCompatibleLocalProvider);
        result.Data["model"].Should().Be("qwen2.5-coder-tool");
        result.Data["model_only_runtime"].Should().Be(true);
        result.Data["fallback_enabled"].Should().Be(false);
    }

    private static OfficialAgentFrameworkHealthCheck CreateHealthCheck(
        AiRoutingOptions? aiRouting = null,
        LlmOptions? llm = null,
        LocalAiOptions? localAi = null,
        IEnumerable<IChatClient>? chatClients = null) =>
        new(
            Options.Create(aiRouting ?? ValidAiRouting()),
            Options.Create(llm ?? new LlmOptions()),
            Options.Create(localAi ?? ValidLocalAi()),
            chatClients ?? [new Mock<IChatClient>().Object]);

    private static AiRoutingOptions ValidAiRouting() => new()
    {
        MicrosoftAgentFrameworkRoutingEnabled = true,
        UseOfficialMicrosoftAgentFramework = true,
        Provider = OfficialAgentProviderFactory.OpenAiCompatibleLocalProvider,
        FallbackToLegacyPipeline = false,
        AllowedDomains = ["model"],
        MaxCandidateTools = 6,
        MaxTotalCandidateTools = 6,
        MaxCandidateToolsPerDomain = 6,
        ToolCallingRequired = true
    };

    private static LocalAiOptions ValidLocalAi() => new()
    {
        BaseUrl = "http://localhost:11434/v1",
        Model = "qwen2.5-coder-tool",
        ApiKeyEnvironmentVariable = "TILSOFTAI_LOCAL_AI_API_KEY",
        TimeoutSeconds = 120
    };
}
