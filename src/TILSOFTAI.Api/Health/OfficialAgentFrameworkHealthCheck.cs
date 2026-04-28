using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;

namespace TILSOFTAI.Api.Health;

/// <summary>
/// Readiness check for the official Microsoft Agent Framework provider path.
/// </summary>
public sealed class OfficialAgentFrameworkHealthCheck : IHealthCheck
{
    private readonly AiRoutingOptions _aiRoutingOptions;
    private readonly LlmOptions _llmOptions;
    private readonly IEnumerable<IChatClient> _chatClients;

    public OfficialAgentFrameworkHealthCheck(
        IOptions<AiRoutingOptions> aiRoutingOptions,
        IOptions<LlmOptions> llmOptions,
        IEnumerable<IChatClient> chatClients)
    {
        _aiRoutingOptions = aiRoutingOptions?.Value ?? throw new ArgumentNullException(nameof(aiRoutingOptions));
        _llmOptions = llmOptions?.Value ?? throw new ArgumentNullException(nameof(llmOptions));
        _chatClients = chatClients ?? throw new ArgumentNullException(nameof(chatClients));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var provider = OfficialAgentProviderFactory.ResolveProviderName(_aiRoutingOptions, _llmOptions);
        var model = OfficialAgentProviderFactory.ResolveModelName(_aiRoutingOptions, _llmOptions);
        var enabled = _aiRoutingOptions.MicrosoftAgentFrameworkRoutingEnabled
            || _aiRoutingOptions.UseOfficialMicrosoftAgentFramework;
        var hasChatClient = _chatClients.Any();
        var data = new Dictionary<string, object>
        {
            ["enabled"] = enabled,
            ["provider"] = string.IsNullOrWhiteSpace(provider) ? "unspecified" : provider,
            ["model"] = string.IsNullOrWhiteSpace(model) ? "unspecified" : model,
            ["chat_client_registered"] = hasChatClient
        };

        if (!enabled)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                "Official Microsoft Agent Framework routing is disabled.",
                data));
        }

        if (!OfficialAgentProviderFactory.IsAllowedProvider(provider))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Official Microsoft Agent Framework routing is enabled, but AiRouting:Provider is not an allowed official provider.",
                data: data));
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Official Microsoft Agent Framework routing is enabled, but no model is configured.",
                data: data));
        }

        if (!hasChatClient)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Official Microsoft Agent Framework routing is enabled, but no IChatClient is registered.",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "Official Microsoft Agent Framework provider is configured.",
            data));
    }
}
