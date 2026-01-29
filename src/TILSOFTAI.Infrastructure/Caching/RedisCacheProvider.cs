using Microsoft.Extensions.Caching.Distributed;
using TILSOFTAI.Domain.Caching;

namespace TILSOFTAI.Infrastructure.Caching;

public sealed class RedisCacheProvider : IRedisCacheProvider
{
    private readonly IDistributedCache _cache;
    private readonly TimeSpan _defaultTtl;

    public RedisCacheProvider(IDistributedCache cache, TimeSpan defaultTtl)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _defaultTtl = defaultTtl;
    }

    public Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        return _cache.GetStringAsync(key, cancellationToken);
    }

    public Task SetStringAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? _defaultTtl
        };

        return _cache.SetStringAsync(key, value, options, cancellationToken);
    }
}
