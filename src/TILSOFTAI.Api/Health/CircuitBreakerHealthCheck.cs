using Microsoft.Extensions.Diagnostics.HealthChecks;
using TILSOFTAI.Infrastructure.Resilience;
using TILSOFTAI.Domain.Resilience;

namespace TILSOFTAI.Api.Health;

/// <summary>
/// Health check that reports circuit breaker states.
/// </summary>
public sealed class CircuitBreakerHealthCheck : IHealthCheck
{
    private readonly CircuitBreakerRegistry _registry;

    public CircuitBreakerHealthCheck(CircuitBreakerRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var states = _registry.GetAllStates();
        var data = new Dictionary<string, object>();
        var openCircuits = new List<string>();
        var halfOpenCircuits = new List<string>();

        foreach (var (name, state) in states)
        {
            data[$"circuit_{name}"] = state.ToString();
            
            if (state == CircuitState.Open)
            {
                openCircuits.Add(name);
            }
            else if (state == CircuitState.HalfOpen)
            {
                halfOpenCircuits.Add(name);
            }
        }

        if (openCircuits.Count > 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Open circuits: {string.Join(", ", openCircuits)}",
                data: data));
        }

        if (halfOpenCircuits.Count > 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Half-open circuits: {string.Join(", ", halfOpenCircuits)}",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "All circuit breakers closed",
            data: data));
    }
}
