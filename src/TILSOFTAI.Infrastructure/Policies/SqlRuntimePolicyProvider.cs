using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Policies;
using TILSOFTAI.Orchestration.Sql;

namespace TILSOFTAI.Infrastructure.Policies;

/// <summary>
/// SQL-backed runtime policy provider with IMemoryCache.
/// Calls app_policy_resolve SP and caches by scope fingerprint.
/// Safe when Redis is disabled.
/// </summary>
public sealed class SqlRuntimePolicyProvider : IRuntimePolicyProvider
{
    private readonly ISqlExecutor _sqlExecutor;
    private readonly IMemoryCache _cache;
    private readonly RuntimePolicySystemOptions _options;
    private readonly ILogger<SqlRuntimePolicyProvider> _logger;

    public SqlRuntimePolicyProvider(
        ISqlExecutor sqlExecutor,
        IMemoryCache cache,
        IOptions<RuntimePolicySystemOptions> options,
        ILogger<SqlRuntimePolicyProvider> logger)
    {
        _sqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RuntimePolicySnapshot> ResolveAsync(
        string tenantId,
        IReadOnlyList<string> moduleKeys,
        string? appKey = null,
        string? environment = null,
        string? language = null,
        CancellationToken ct = default)
    {
        var env = environment ?? _options.Environment;
        var cacheKey = $"policy:{tenantId}:{string.Join(",", moduleKeys)}:{appKey}:{env}:{language}";

        if (_cache.TryGetValue<RuntimePolicySnapshot>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var moduleKeysJson = JsonSerializer.Serialize(moduleKeys);
            var parameters = new Dictionary<string, object?>
            {
                ["@TenantId"] = tenantId,
                ["@ModuleKeysJson"] = moduleKeysJson,
                ["@AppKey"] = appKey,
                ["@Environment"] = env,
                ["@Language"] = language
            };

            var rows = await _sqlExecutor.ExecuteQueryAsync("dbo.app_policy_resolve", parameters, ct);

            var policies = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                if (row.TryGetValue("PolicyKey", out var keyObj) && keyObj is string policyKey
                    && row.TryGetValue("JsonValue", out var valueObj) && valueObj is string jsonValue)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(jsonValue);
                        policies[policyKey] = doc.RootElement.Clone();
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Invalid JSON in RuntimePolicy for key {PolicyKey}", policyKey);
                    }
                }
            }

            var snapshot = new RuntimePolicySnapshot(policies);

            _cache.Set(cacheKey, snapshot, TimeSpan.FromSeconds(_options.CacheTtlSeconds));

            _logger.LogDebug(
                "PolicyResolved | TenantId: {TenantId} | Modules: [{Modules}] | PolicyCount: {Count}",
                tenantId, string.Join(", ", moduleKeys), policies.Count);

            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve runtime policies. Returning empty snapshot.");
            return RuntimePolicySnapshot.Empty;
        }
    }
}
