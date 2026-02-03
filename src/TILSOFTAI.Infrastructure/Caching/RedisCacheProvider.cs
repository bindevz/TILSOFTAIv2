using Microsoft.Extensions.Caching.Distributed;
using TILSOFTAI.Domain.Caching;
using TILSOFTAI.Domain.Metrics;

namespace TILSOFTAI.Infrastructure.Caching;

public sealed class RedisCacheProvider : IRedisCacheProvider
{
    private readonly IDistributedCache _cache;
    private readonly TimeSpan _defaultTtl;

    private readonly IMetricsService _metrics;
    private readonly TILSOFTAI.Domain.Resilience.ICircuitBreakerPolicy _circuitBreaker;
    private readonly TILSOFTAI.Domain.Resilience.IRetryPolicy _retryPolicy;

    public RedisCacheProvider(IDistributedCache cache, TimeSpan defaultTtl, IMetricsService metrics, TILSOFTAI.Infrastructure.Resilience.CircuitBreakerRegistry registry, TILSOFTAI.Infrastructure.Resilience.RetryPolicyRegistry retryRegistry)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _defaultTtl = defaultTtl;
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _circuitBreaker = registry?.GetOrCreate("redis") ?? throw new ArgumentNullException(nameof(registry));
        _retryPolicy = retryRegistry?.GetOrCreate("redis") ?? throw new ArgumentNullException(nameof(retryRegistry));
    }

    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
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

    public Task SetStringAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? _defaultTtl
        };

        return _circuitBreaker.ExecuteAsync<bool>(async ct =>
        {
            return await _retryPolicy.ExecuteAsync<bool>(async (int attempt, CancellationToken token) =>
            {
                await _cache.SetStringAsync(key, value, options, token);
                return true; // dummy return for Void
            }, ct);
        }, cancellationToken);
    }
}
