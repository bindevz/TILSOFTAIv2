using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Api.Options;

/// <summary>
/// Configures RedisCacheOptions using validated IOptions&lt;RedisOptions&gt;.
/// </summary>
public sealed class ConfigureRedisCacheOptions : IConfigureOptions<RedisCacheOptions>
{
    private readonly IOptions<RedisOptions> _redisOptions;

    public ConfigureRedisCacheOptions(IOptions<RedisOptions> redisOptions)
    {
        _redisOptions = redisOptions ?? throw new ArgumentNullException(nameof(redisOptions));
    }

    public void Configure(RedisCacheOptions options)
    {
        var redisOpts = _redisOptions.Value;
        options.Configuration = redisOpts.ConnectionString;
    }
}
