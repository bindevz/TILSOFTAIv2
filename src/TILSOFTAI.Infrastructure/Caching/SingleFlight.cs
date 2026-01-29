using System.Collections.Concurrent;

namespace TILSOFTAI.Infrastructure.Caching;

public sealed class CacheStampedeGuard
{
    private readonly ConcurrentDictionary<string, Lazy<Task<object?>>> _inflight = new();

    public async Task<T> RunAsync<T>(string key, Func<Task<T>> factory)
    {
        var lazy = _inflight.GetOrAdd(key, _ => new Lazy<Task<object?>>(async () => await factory().ConfigureAwait(false)));

        try
        {
            var result = await lazy.Value.ConfigureAwait(false);
            return result is T typed ? typed : default!;
        }
        finally
        {
            _inflight.TryRemove(key, out _);
        }
    }
}
