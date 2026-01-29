namespace TILSOFTAI.Domain.Caching;

public interface IRedisCacheProvider
{
    Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default);
    Task SetStringAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
}
