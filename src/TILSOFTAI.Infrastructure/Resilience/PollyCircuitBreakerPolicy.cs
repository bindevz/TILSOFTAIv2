using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Domain.Resilience;

namespace TILSOFTAI.Infrastructure.Resilience;

public class PollyCircuitBreakerPolicy : TILSOFTAI.Domain.Resilience.ICircuitBreakerPolicy
{
    private readonly AsyncCircuitBreakerPolicy _policy;
    private readonly string _name;
    private readonly ILogger _logger;
    private readonly IMetricsService _metrics;

    public string CircuitName => _name;

    public TILSOFTAI.Domain.Resilience.CircuitState State => _policy.CircuitState switch
    {
        Polly.CircuitBreaker.CircuitState.Closed => TILSOFTAI.Domain.Resilience.CircuitState.Closed,
        Polly.CircuitBreaker.CircuitState.Open => TILSOFTAI.Domain.Resilience.CircuitState.Open,
        Polly.CircuitBreaker.CircuitState.HalfOpen => TILSOFTAI.Domain.Resilience.CircuitState.HalfOpen,
        Polly.CircuitBreaker.CircuitState.Isolated => TILSOFTAI.Domain.Resilience.CircuitState.Isolated,
        _ => TILSOFTAI.Domain.Resilience.CircuitState.Closed
    };

    public PollyCircuitBreakerPolicy(string name, CircuitBreakerOptions options, ILogger logger, IMetricsService metrics)
    {
        _name = name;
        _logger = logger;
        _metrics = metrics;

        _policy = Policy
            .Handle<Exception>(ex => !IsExcludedException(ex)) // Handle all exceptions except excluded ones
            .AdvancedCircuitBreakerAsync(
                samplingDuration: options.SamplingDuration,
                failureThreshold: 0.5, // Break if 50% of requests fail
                minimumThroughput: options.FailureThreshold, // Minimum requests in window before breaking
                durationOfBreak: options.BreakDuration,
                onBreak: (ex, breakDelay) =>
                {
                    _logger.LogWarning("Circuit {CircuitName} OPENED for {BreakDelay}s due to: {Exception}", _name, breakDelay.TotalSeconds, ex.Message);
                    _metrics.RecordGauge("tilsoftai_circuit_state", 1, new() { { "name", _name } });
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit {CircuitName} CLOSED (Recovered)", _name);
                    _metrics.RecordGauge("tilsoftai_circuit_state", 0, new() { { "name", _name } });
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit {CircuitName} HALF-OPEN (Probing)", _name);
                    _metrics.RecordGauge("tilsoftai_circuit_state", 2, new() { { "name", _name } });
                }
            );
    }

    private bool IsExcludedException(Exception ex)
    {
        // Add logic to exclude specific exceptions if needed (e.g., OperationCanceledException might be excluded depending on policy)
        return false;
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        try
        {
            if (State == TILSOFTAI.Domain.Resilience.CircuitState.Open)
            {
                throw new BrokenCircuitException("Circuit is open.");
            }

            return await _policy.ExecuteAsync(async (token) => await action(token), ct);
        }
        catch (BrokenCircuitException ex)
        {
            throw new CircuitBreakerException(_name, $"Circuit '{_name}' is OPEN. Operation rejected.", ex);
        }
    }
}
