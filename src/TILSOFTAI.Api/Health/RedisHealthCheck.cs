using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Api.Health;

/// <summary>
/// Health check for Redis connectivity.
/// Only runs when Redis is enabled in configuration.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly RedisOptions _redisOptions;
    private readonly IDistributedCache _cache;

    public RedisHealthCheck(
        IOptions<RedisOptions> redisOptions,
        IDistributedCache cache)
    {
        _redisOptions = redisOptions?.Value ?? throw new ArgumentNullException(nameof(redisOptions));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_redisOptions.Enabled)
        {
            return HealthCheckResult.Healthy("Redis is disabled");
        }

        try
        {
            // Try to set and get a test value
            var testKey = "__health_check__";
            var testValue = DateTimeOffset.UtcNow.Ticks.ToString();
            
            await _cache.SetStringAsync(testKey, testValue, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5)
            }, cancellationToken);
            
            var retrieved = await _cache.GetStringAsync(testKey, cancellationToken);
            
            if (retrieved == testValue)
            {
                return HealthCheckResult.Healthy("Redis is accessible");
            }
            
            return HealthCheckResult.Degraded("Redis returned unexpected value");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis is not accessible", ex);
        }
    }
}
