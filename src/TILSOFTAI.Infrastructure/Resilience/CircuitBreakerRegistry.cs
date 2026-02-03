using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Domain.Resilience;

namespace TILSOFTAI.Infrastructure.Resilience;

public class CircuitBreakerRegistry
{
    private readonly ConcurrentDictionary<string, ICircuitBreakerPolicy> _policies = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly IMetricsService _metrics;
    private readonly ResilienceOptions _options;

    public CircuitBreakerRegistry(
        ILoggerFactory loggerFactory,
        IMetricsService metrics,
        IOptions<ResilienceOptions> options)
    {
        _loggerFactory = loggerFactory;
        _metrics = metrics;
        _options = options.Value;
    }

    public ICircuitBreakerPolicy GetOrCreate(string name, CircuitBreakerOptions? optionsOverride = null)
    {
        return _policies.GetOrAdd(name, _ =>
        {
            var options = optionsOverride ?? GetOptionsFor(name);
            return new PollyCircuitBreakerPolicy(
                name,
                options,
                _loggerFactory.CreateLogger<PollyCircuitBreakerPolicy>(),
                _metrics);
        });
    }

    private CircuitBreakerOptions GetOptionsFor(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "llm" => _options.LlmCircuitBreaker,
            "sql" => _options.SqlCircuitBreaker,
            "redis" => _options.RedisCircuitBreaker,
            _ => new CircuitBreakerOptions()
        };
    }

    public CircuitState GetState(string name)
    {
        if (_policies.TryGetValue(name, out var policy))
        {
            return policy.State;
        }
        return CircuitState.Closed; // Default if not found/initialized
    }

    public Dictionary<string, CircuitState> GetAllStates()
    {
        return _policies.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.State);
    }
}
