using TILSOFTAI.Domain.Caching;

namespace TILSOFTAI.Infrastructure.Caching;

public sealed class NullRedisCacheProvider : IRedisCacheProvider
{
    public Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    public Task SetStringAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
