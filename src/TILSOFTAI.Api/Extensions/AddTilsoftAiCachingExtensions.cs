using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Options;
using TILSOFTAI.Domain.Caching;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Infrastructure.Caching;

namespace TILSOFTAI.Api.Extensions;

public static class AddTilsoftAiCachingExtensions
{
    public static IServiceCollection AddTilsoftAiCaching(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddSingleton<CacheStampedeGuard>();

        var redisEnabled = configuration.GetValue<bool>("Redis:Enabled");
        if (redisEnabled)
        {
            services.AddSingleton<IConfigureOptions<RedisCacheOptions>, ConfigureRedisCacheOptions>();
            services.AddStackExchangeRedisCache(_ => { });

            services.AddSingleton<IRedisCacheProvider>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
                var cache = sp.GetRequiredService<IDistributedCache>();
                var metrics = sp.GetRequiredService<IMetricsService>();
                var circuitry = sp.GetRequiredService<TILSOFTAI.Infrastructure.Resilience.CircuitBreakerRegistry>();
                var retries = sp.GetRequiredService<TILSOFTAI.Infrastructure.Resilience.RetryPolicyRegistry>();
                var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<RedisCacheProvider>();
                return new RedisCacheProvider(cache, TimeSpan.FromMinutes(options.DefaultTtlMinutes), metrics, circuitry, retries, logger);
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
            services.AddSingleton<IRedisCacheProvider, NullRedisCacheProvider>();
        }

        return services;
    }
}
