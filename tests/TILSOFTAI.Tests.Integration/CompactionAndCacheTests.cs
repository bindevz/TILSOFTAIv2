using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Caching;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Infrastructure.Caching;
using TILSOFTAI.Orchestration.Compaction;
using TILSOFTAI.Orchestration.Tools;
using Xunit;

namespace TILSOFTAI.Tests.Integration;

public sealed class CompactionAndCacheTests
{
    [Fact]
    public void Compactor_RemovesAuditFields()
    {
        var compactionRules = new CompactionRules
        {
            RemoveFields = ["CreatedAtUtc", "UpdatedAtUtc"]
        };

        var compactor = new ToolResultCompactor();
        var raw = """
{
  "rows": [
    { "id": 1, "CreatedAtUtc": "2025-01-01T00:00:00Z", "value": 10 }
  ]
}
""";

        var compacted = compactor.CompactJson(raw, 4096, compactionRules);

        using var doc = JsonDocument.Parse(compacted);
        var row = doc.RootElement.GetProperty("rows")[0];
        Assert.False(row.TryGetProperty("CreatedAtUtc", out _));
        Assert.Equal(10, row.GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task SemanticCache_ReturnsCachedAnswer()
    {
        var redisOptions = Options.Create(new RedisOptions
        {
            Enabled = true,
            DefaultTtlMinutes = 30
        });
        var cacheOptions = Options.Create(new SemanticCacheOptions
        {
            Enabled = true,
            AllowSensitiveContent = false
        });
        var sqlOptions = Options.Create(new SqlOptions
        {
            ConnectionString = "Server=.;Database=TILSOFTAI;Trusted_Connection=True;TrustServerCertificate=True;",
            CommandTimeoutSeconds = 30
        });
        var sensitiveOptions = Options.Create(new SensitiveDataOptions
        {
            HandlingMode = SensitiveHandlingMode.Redact,
            DisableCachingWhenSensitive = true,
            DisableToolResultPersistenceWhenSensitive = true
        });
        var memoryCache = new InMemoryRedisCacheProvider();
        var cache = new SemanticCache(
            memoryCache,
            new CacheStampedeGuard(),
            redisOptions,
            cacheOptions,
            sensitiveOptions,
            sqlOptions,
            NullLogger<SemanticCache>.Instance);

        var context = new TilsoftExecutionContext
        {
            TenantId = "tenant-a",
            Language = "en"
        };
        var tools = new List<ToolDefinition>();

        await cache.SetAnswerAsync(context, "core", "hello", tools, null, "cached", false, CancellationToken.None);
        var answer = await cache.TryGetAnswerAsync(context, "core", "hello", tools, null, false, CancellationToken.None);

        Assert.Equal("cached", answer);
    }

    private sealed class InMemoryRedisCacheProvider : IRedisCacheProvider
    {
        private readonly Dictionary<string, string> _store = new(StringComparer.OrdinalIgnoreCase);

        public Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(key, out var value);
            return Task.FromResult<string?>(value);
        }

        public Task SetStringAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }
    }
}
