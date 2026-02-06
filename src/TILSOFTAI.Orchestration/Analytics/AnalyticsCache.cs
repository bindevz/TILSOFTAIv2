using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Analytics;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Orchestration.Analytics;

/// <summary>
/// PATCH 31.05: Item in the cache write background queue.
/// </summary>
public sealed record CacheWriteItem(
    string TenantId,
    string NormalizedQuery,
    IEnumerable<string>? Roles,
    InsightOutput Insight);

/// <summary>
/// PATCH 31.05: Queue for background cache writes.
/// </summary>
public interface ICacheWriteQueue
{
    /// <summary>
    /// Enqueue a cache write. Non-blocking, returns false if queue is full.
    /// </summary>
    bool TryEnqueue(CacheWriteItem item);
}

/// <summary>
/// PATCH 29.06: Caches InsightOutput for repeated queries.
/// PATCH 30.03: Include roles hash in cache key to prevent privilege leakage.
/// </summary>
public sealed class AnalyticsCache
{
    private readonly SqlOptions _sqlOptions;
    private readonly AnalyticsOptions _analyticsOptions;
    private readonly ILogger<AnalyticsCache> _logger;

    public AnalyticsCache(
        IOptions<SqlOptions> sqlOptions,
        IOptions<AnalyticsOptions> analyticsOptions,
        ILogger<AnalyticsCache> logger)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
        _analyticsOptions = analyticsOptions?.Value ?? throw new ArgumentNullException(nameof(analyticsOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Tries to get a cached insight.
    /// PATCH 30.03: Requires roles to ensure cache isolation by security context.
    /// </summary>
    public async Task<InsightOutput?> TryGetAsync(
        string tenantId, 
        string normalizedQuery, 
        IEnumerable<string>? roles,
        CancellationToken ct)
    {
        if (!_analyticsOptions.EnableInsightCache)
        {
            return null;
        }

        try
        {
            var queryHash = ComputeHash(tenantId, normalizedQuery, roles);

            await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.app_analytics_insightcache_get";
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TenantId", tenantId);
            command.Parameters.AddWithValue("@QueryHash", queryHash);

            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var insightJson = reader.GetString(0);
                var hitCount = reader.GetInt32(3);

                _logger.LogInformation(
                    "CacheHit | TenantId: {TenantId} | QueryHash: {QueryHash} | HitCount: {HitCount}",
                    tenantId, queryHash, hitCount);

                return JsonSerializer.Deserialize<InsightOutput>(insightJson);
            }

            return null;
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Cache get failed | TenantId: {TenantId}", tenantId);
            return null;
        }
    }

    /// <summary>
    /// Sets a cached insight.
    /// PATCH 30.03: Requires roles to ensure cache isolation by security context.
    /// </summary>
    public async Task SetAsync(
        string tenantId, 
        string normalizedQuery, 
        IEnumerable<string>? roles,
        InsightOutput insight, 
        CancellationToken ct)
    {
        if (!_analyticsOptions.EnableInsightCache)
        {
            return;
        }

        // PATCH 30.03: Optional bypass for restricted-tag results
        if (!_analyticsOptions.AllowCachingRestricted && ContainsSecurityWarning(insight))
        {
            _logger.LogDebug("CacheBypass | Security-sensitive result not cached | TenantId: {TenantId}", tenantId);
            return;
        }

        try
        {
            var queryHash = ComputeHash(tenantId, normalizedQuery, roles);
            var insightJson = JsonSerializer.Serialize(insight);

            await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.app_analytics_insightcache_set";
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TenantId", tenantId);
            command.Parameters.AddWithValue("@QueryHash", queryHash);
            command.Parameters.AddWithValue("@HeadlineText", insight.Headline?.Text ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@InsightJson", insightJson);
            command.Parameters.AddWithValue("@DataFreshnessUtc", insight.Freshness?.AsOfUtc ?? DateTime.UtcNow);
            command.Parameters.AddWithValue("@TtlSeconds", _analyticsOptions.InsightCacheTtlSeconds);

            await command.ExecuteNonQueryAsync(ct);
            _logger.LogDebug("CacheSet | TenantId: {TenantId} | QueryHash: {QueryHash}", tenantId, queryHash);
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Cache set failed | TenantId: {TenantId}", tenantId);
        }
    }

    /// <summary>
    /// PATCH 30.03: Check if insight contains security-related warnings.
    /// </summary>
    private static bool ContainsSecurityWarning(InsightOutput insight)
    {
        if (insight.Notes == null) return false;
        
        var securityTerms = new[] { "restricted", "security", "pii", "sensitive" };
        return insight.Notes.Any(note => 
            securityTerms.Any(term => 
                note.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// PATCH 30.03: Computes a deterministic hash including roles for security.
    /// Key = sha256(tenantId|normalizedQuery|rolesCsv)
    /// </summary>
    public static string ComputeHash(string tenantId, string normalizedQuery, IEnumerable<string>? roles)
    {
        // Normalize roles: sorted, unique, lowercase, joined
        var rolesCsv = roles != null
            ? string.Join(",", roles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim().ToLowerInvariant())
                .Distinct()
                .OrderBy(r => r, StringComparer.Ordinal))
            : string.Empty;
        
        var input = $"{tenantId}|{normalizedQuery}|{rolesCsv}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Legacy overload for backward compatibility.
    /// </summary>
    [Obsolete("Use ComputeHash with roles parameter for security")]
    public static string ComputeHash(string tenantId, string normalizedQuery)
    {
        return ComputeHash(tenantId, normalizedQuery, null);
    }
}
