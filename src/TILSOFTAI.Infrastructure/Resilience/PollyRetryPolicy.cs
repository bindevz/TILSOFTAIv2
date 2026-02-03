using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Polly.Contrib.WaitAndRetry;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Domain.Resilience;

namespace TILSOFTAI.Infrastructure.Resilience;

public class PollyRetryPolicy : TILSOFTAI.Domain.Resilience.IRetryPolicy
{
    private readonly AsyncRetryPolicy _policy;
    private readonly string _name;
    private readonly ILogger _logger;
    private readonly IMetricsService _metrics;

    public PollyRetryPolicy(string name, RetryOptions options, ILogger logger, IMetricsService metrics)
    {
        _name = name;
        _logger = logger;
        _metrics = metrics;

        // Exponential backoff with jitter
        var delay = Polly.Contrib.WaitAndRetry.Backoff.DecorrelatedJitterBackoffV2(
            medianFirstRetryDelay: options.InitialDelay,
            retryCount: options.MaxRetries
        );

        _policy = Policy
            .Handle<Exception>(ex => TransientExceptionClassifier.IsTransient(ex))
            .WaitAndRetryAsync(
                delay,
                onRetry: (ex, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(ex, "Retry {RetryCount} for {Dependency} after {Delay}ms. Error: {Message}", 
                        retryCount, _name, timeSpan.TotalMilliseconds, ex.Message);
                    
                    _metrics.IncrementCounter(MetricNames.RetryAttemptsTotal, new() 
                    { 
                        { "dependency", _name },
                        { "outcome", "retry" }
                    });
                }
            );
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        // Internal delegate to match Polly's expectation
        return await _policy.ExecuteAsync<T>(async (ct) => await action(ct), cancellationToken);
    }
    
    public async Task<T> ExecuteAsync<T>(Func<int, CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        // Capture attempt logic if needed, but Polly manages attempts.
        // If we want to pass the attempt number to the action, we might need a custom policy or context.
        // Standard Polly ExecuteAsync doesn't pass attempt count to the action directly unless we use Context.
        // For simple compatibility, we can just invoke it.
        // However, the interface demands it. 
        // We can use a simplified approach: The action takes attempt count.
        // We can execute inside the policy context.
        
        // Actually, to correctly support passing attempt count to the action using standard Polly,
        // we'd need to manually track it or access it via Context if we set it up.
        // But standard WaitAndRetryAsync doesn't easily inject attempt count into the *action* itself, only the *onRetry* callback.
        
        // A workaround is to use a captured variable context, but that's not thread safe if reusing policy instance?
        // Wait, Policy instance is stateless regarding execution state (except CircuitBreaker). Retry is stateless.
        // So we can do:
        
        int attempt = 0;
        return await _policy.ExecuteAsync<T>(async (ct) => 
        {
            // increment attempts? No, this will be called initial + retries.
            // On first call attempt=0. On first retry attempt=1.
            var currentAttempt = attempt++;
            return await action(currentAttempt, ct);
        }, cancellationToken);
    }
}
