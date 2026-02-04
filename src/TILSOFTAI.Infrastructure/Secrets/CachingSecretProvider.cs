using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Secrets;

namespace TILSOFTAI.Infrastructure.Secrets;

/// <summary>
/// Decorator that adds caching to any secret provider.
/// </summary>
public sealed class CachingSecretProvider : ISecretProvider
{
    private readonly ISecretProvider _inner;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration;

    public string ProviderName => $"Cached({_inner.ProviderName})";

    public CachingSecretProvider(
        ISecretProvider inner,
        IMemoryCache cache,
        IOptions<SecretsOptions> options)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheDuration = TimeSpan.FromSeconds(options?.Value?.CacheSeconds ?? 300);
    }

    public async Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"secret:{key}";
        
        if (_cache.TryGetValue(cacheKey, out string? cached))
        {
            return cached;
        }

        var value = await _inner.GetSecretAsync(key, cancellationToken);
        
        if (value is not null)
        {
            _cache.Set(cacheKey, value, _cacheDuration);
        }

        return value;
    }

    public Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
        => _inner.SecretExistsAsync(key, cancellationToken);
}
