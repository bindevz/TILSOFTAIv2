using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Domain.Caching;
using TILSOFTAI.Domain.Metrics;

namespace TILSOFTAI.Infrastructure.Caching;

public sealed class RedisCacheProvider : IRedisCacheProvider
{
    private readonly IDistributedCache _cache;
    private readonly TimeSpan _defaultTtl;

    private readonly IMetricsService _metrics;
    private readonly ILogger<RedisCacheProvider> _logger;
    private readonly TILSOFTAI.Domain.Resilience.ICircuitBreakerPolicy _circuitBreaker;
    private readonly TILSOFTAI.Domain.Resilience.IRetryPolicy _retryPolicy;

    public RedisCacheProvider(
        IDistributedCache cache,
        TimeSpan defaultTtl,
        IMetricsService metrics,
        TILSOFTAI.Infrastructure.Resilience.CircuitBreakerRegistry registry,
        TILSOFTAI.Infrastructure.Resilience.RetryPolicyRegistry retryRegistry,
        ILogger<RedisCacheProvider> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _defaultTtl = defaultTtl;
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _circuitBreaker = registry?.GetOrCreate("redis") ?? throw new ArgumentNullException(nameof(registry));
        _retryPolicy = retryRegistry?.GetOrCreate("redis") ?? throw new ArgumentNullException(nameof(retryRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _circuitBreaker.ExecuteAsync<string?>(async ct => 
            {
                return await _retryPolicy.ExecuteAsync<string?>(async (int attempt, CancellationToken token) =>
                {
                    return await _cache.GetStringAsync(key, token);
                }, ct);
            }, cancellationToken);

            if (value != null)
            {
                _metrics.IncrementCounter(MetricNames.CacheHitsTotal);
            }
            else
            {
                _metrics.IncrementCounter(MetricNames.CacheMissesTotal);
            }
        
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis GET failed for key '{Key}'. Degrading gracefully.", key);
            _metrics.IncrementCounter(MetricNames.CacheMissesTotal);
            return null;
        }
    }

    public async Task SetStringAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? _defaultTtl
        };

        try
        {
            await _circuitBreaker.ExecuteAsync<bool>(async ct =>
            {
                return await _retryPolicy.ExecuteAsync<bool>(async (int attempt, CancellationToken token) =>
                {
                    await _cache.SetStringAsync(key, value, options, token);
                    return true; // dummy return for Void
                }, ct);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SET failed for key '{Key}'. Degrading gracefully.", key);
        }
    }
}

