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
/// PATCH 29.06: Caches InsightOutput for repeated queries.
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
    /// </summary>
    public async Task<InsightOutput?> TryGetAsync(string tenantId, string normalizedQuery, CancellationToken ct)
    {
        if (!_analyticsOptions.EnableInsightCache)
        {
            return null;
        }

        try
        {
            var queryHash = ComputeHash(tenantId, normalizedQuery);

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
    /// </summary>
    public async Task SetAsync(string tenantId, string normalizedQuery, InsightOutput insight, CancellationToken ct)
    {
        if (!_analyticsOptions.EnableInsightCache)
        {
            return;
        }

        try
        {
            var queryHash = ComputeHash(tenantId, normalizedQuery);
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
    /// Computes a deterministic hash for the query key.
    /// </summary>
    public static string ComputeHash(string tenantId, string normalizedQuery)
    {
        var input = $"{tenantId}:{normalizedQuery}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
