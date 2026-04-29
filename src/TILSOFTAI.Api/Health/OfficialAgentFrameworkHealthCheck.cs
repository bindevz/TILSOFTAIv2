using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Api.Health;

/// <summary>
/// Readiness check for the official Microsoft Agent Framework provider path.
/// </summary>
public sealed class OfficialAgentFrameworkHealthCheck : IHealthCheck
{
    private const string PlaceholderLocalModel = "CHANGE_ME_TOOL_CALLING_MODEL";

    private readonly AiRoutingOptions _aiRoutingOptions;
    private readonly LlmOptions _llmOptions;
    private readonly LocalAiOptions _localAiOptions;
    private readonly IEnumerable<IChatClient> _chatClients;

    public OfficialAgentFrameworkHealthCheck(
        IOptions<AiRoutingOptions> aiRoutingOptions,
        IOptions<LlmOptions> llmOptions,
        IOptions<LocalAiOptions> localAiOptions,
        IEnumerable<IChatClient> chatClients)
    {
        _aiRoutingOptions = aiRoutingOptions?.Value ?? throw new ArgumentNullException(nameof(aiRoutingOptions));
        _llmOptions = llmOptions?.Value ?? throw new ArgumentNullException(nameof(llmOptions));
        _localAiOptions = localAiOptions?.Value ?? throw new ArgumentNullException(nameof(localAiOptions));
        _chatClients = chatClients ?? throw new ArgumentNullException(nameof(chatClients));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var provider = OfficialAgentProviderFactory.ResolveProviderName(_aiRoutingOptions, _llmOptions);
        var model = string.Equals(provider, OfficialAgentProviderFactory.OpenAiCompatibleLocalProvider, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(_localAiOptions.Model)
                ? _localAiOptions.Model
                : OfficialAgentProviderFactory.ResolveModelName(_aiRoutingOptions, _llmOptions);
        var enabled = _aiRoutingOptions.MicrosoftAgentFrameworkRoutingEnabled
            || _aiRoutingOptions.UseOfficialMicrosoftAgentFramework;
        var hasChatClient = _chatClients.Any();
        var isLocalAiProvider = string.Equals(provider, OfficialAgentProviderFactory.OpenAiCompatibleLocalProvider, StringComparison.OrdinalIgnoreCase);
        var isLocalAiModelPlaceholder = string.Equals(_localAiOptions.Model?.Trim(), PlaceholderLocalModel, StringComparison.OrdinalIgnoreCase);
        var isModelOnlyRuntime = IsModelOnlyRuntime(_aiRoutingOptions.AllowedDomains);
        var data = new Dictionary<string, object>
        {
            ["enabled"] = enabled,
            ["provider"] = string.IsNullOrWhiteSpace(provider) ? "unspecified" : provider,
            ["model"] = string.IsNullOrWhiteSpace(model) ? "unspecified" : model,
            ["allowed_domains"] = string.Join(",", _aiRoutingOptions.AllowedDomains),
            ["max_candidate_tools"] = _aiRoutingOptions.MaxCandidateTools,
            ["fallback_enabled"] = _aiRoutingOptions.FallbackToLegacyPipeline,
            ["tool_calling_required"] = _aiRoutingOptions.ToolCallingRequired,
            ["model_only_runtime"] = isModelOnlyRuntime,
            ["local_ai_model_placeholder"] = isLocalAiModelPlaceholder,
            ["base_url_source"] = string.Equals(provider, OfficialAgentProviderFactory.OpenAiCompatibleLocalProvider, StringComparison.OrdinalIgnoreCase)
                ? "LocalAi:BaseUrl"
                : "Llm:Endpoint",
            ["chat_client_registered"] = hasChatClient
        };

        if (!enabled)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                "Official Microsoft Agent Framework routing is disabled.",
                data));
        }

        if (string.IsNullOrWhiteSpace(_aiRoutingOptions.Provider))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Official Microsoft Agent Framework routing is enabled, but AiRouting:Provider is not configured.",
                data: data));
        }

        if (!OfficialAgentProviderFactory.IsAllowedProvider(provider))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Official Microsoft Agent Framework routing is enabled, but AiRouting:Provider is not an allowed official provider.",
                data: data));
        }

        if (!isModelOnlyRuntime)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Official Microsoft Agent Framework routing is enabled, but AiRouting:AllowedDomains must contain only the model domain for Sprint 35.",
                data: data));
        }

        if (_aiRoutingOptions.FallbackToLegacyPipeline)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Official Microsoft Agent Framework routing is enabled, but AiRouting:FallbackToLegacyPipeline is still enabled.",
                data: data));
        }

        if (isLocalAiProvider && string.IsNullOrWhiteSpace(_localAiOptions.Model))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Official Microsoft Agent Framework routing is enabled for OpenAiCompatibleLocal, but LocalAi:Model is not configured.",
                data: data));
        }

        if (isLocalAiProvider && isLocalAiModelPlaceholder)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Official Microsoft Agent Framework routing is enabled for OpenAiCompatibleLocal, but LocalAi:Model still contains the placeholder value.",
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

    private static bool IsModelOnlyRuntime(IEnumerable<string>? allowedDomains)
    {
        var normalizedDomains = (allowedDomains ?? Array.Empty<string>())
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Select(DomainGate.NormalizeDomain)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalizedDomains.Length == 1
            && string.Equals(normalizedDomains[0], "model", StringComparison.OrdinalIgnoreCase);
    }
}
