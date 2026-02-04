using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Llm;

namespace TILSOFTAI.Api.Health;

/// <summary>
/// Health check for LLM endpoint availability.
/// </summary>
public sealed class LlmHealthCheck : IHealthCheck
{
    private readonly ILlmClient _llmClient;
    private readonly LlmOptions _options;
    private readonly ILogger<LlmHealthCheck> _logger;
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    public LlmHealthCheck(
        ILlmClient llmClient,
        IOptions<LlmOptions> options,
        ILogger<LlmHealthCheck> logger)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Skip check if using Null provider (offline mode)
        if (_options.Provider == "Null")
        {
            return HealthCheckResult.Healthy("LLM provider is Null (offline mode)");
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            // Attempt a lightweight request to verify connectivity
            // Most LLM APIs support a models list endpoint
            var isHealthy = await _llmClient.PingAsync(cts.Token);

            if (isHealthy)
            {
                return HealthCheckResult.Healthy($"LLM endpoint reachable: {_options.Provider}");
            }

            return HealthCheckResult.Degraded("LLM endpoint responded but may have issues");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("LLM health check timed out");
            return HealthCheckResult.Unhealthy("LLM endpoint timeout");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM health check failed");
            return HealthCheckResult.Unhealthy($"LLM endpoint unreachable: {ex.Message}");
        }
    }
}
