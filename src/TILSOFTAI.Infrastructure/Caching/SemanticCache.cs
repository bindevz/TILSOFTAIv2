using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Caching;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Caching;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Infrastructure.Caching;

public sealed class SemanticCache : ISemanticCache
{
    private readonly IRedisCacheProvider _cacheProvider;
    private readonly CacheStampedeGuard _stampedeGuard;
    private readonly RedisOptions _redisOptions;
    private readonly SemanticCacheOptions _options;
    private readonly SqlOptions _sqlOptions;
    private readonly ILogger<SemanticCache> _logger;
    private readonly TimeSpan _defaultTtl;

    public SemanticCache(
        IRedisCacheProvider cacheProvider,
        CacheStampedeGuard stampedeGuard,
        IOptions<RedisOptions> redisOptions,
        IOptions<SemanticCacheOptions> options,
        IOptions<SqlOptions> sqlOptions,
        ILogger<SemanticCache> logger)
    {
        _cacheProvider = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));
        _stampedeGuard = stampedeGuard ?? throw new ArgumentNullException(nameof(stampedeGuard));
        _redisOptions = redisOptions?.Value ?? throw new ArgumentNullException(nameof(redisOptions));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var ttlMinutes = Math.Max(30, _redisOptions.DefaultTtlMinutes);
        _defaultTtl = TimeSpan.FromMinutes(ttlMinutes);
    }

    public bool Enabled => _options.Enabled;

    public async Task<string?> TryGetAnswerAsync(
        TilsoftExecutionContext context,
        string module,
        string question,
        IReadOnlyList<ToolDefinition> tools,
        string? planJson,
        bool containsSensitive,
        CancellationToken ct)
    {
        if (!CanCache(containsSensitive))
        {
            return null;
        }

        var (digest, _, _, _) = ComputeCacheDigests(context, question, tools, planJson);

        if (_options.EnableSqlVectorCache)
        {
            var sqlAnswer = await TryGetSqlCacheAsync(context, module, digest, ct);
            if (!string.IsNullOrWhiteSpace(sqlAnswer))
            {
                return sqlAnswer;
            }
        }

        var key = BuildKey(context.TenantId, module, "semantic_answer", digest);

        return await _cacheProvider.GetStringAsync(key, ct);
    }

    public async Task SetAnswerAsync(
        TilsoftExecutionContext context,
        string module,
        string question,
        IReadOnlyList<ToolDefinition> tools,
        string? planJson,
        string answer,
        bool containsSensitive,
        CancellationToken ct)
    {
        if (!CanCache(containsSensitive))
        {
            return;
        }

        var (digest, questionHash, toolDigest, planDigest) = ComputeCacheDigests(context, question, tools, planJson);

        if (_options.EnableSqlVectorCache)
        {
            await TrySetSqlCacheAsync(context, module, digest, questionHash, toolDigest, planDigest, answer, ct);
        }

        var key = BuildKey(context.TenantId, module, "semantic_answer", digest);
        await _cacheProvider.SetStringAsync(key, answer, _defaultTtl, ct);
    }

    public async Task<T> GetOrAddAsync<T>(
        string key,
        Func<Task<T>> factory,
        Func<T, string> serializer,
        Func<string, T?> deserializer,
        CancellationToken ct)
    {
        if (!Enabled)
        {
            return await factory();
        }

        var cached = await _cacheProvider.GetStringAsync(key, ct);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            var value = deserializer(cached);
            if (value is not null)
            {
                return value;
            }
        }

        return await _stampedeGuard.RunAsync(key, async () =>
        {
            var computed = await factory();
            var serialized = serializer(computed);
            if (!string.IsNullOrWhiteSpace(serialized))
            {
                await _cacheProvider.SetStringAsync(key, serialized, _defaultTtl, ct);
            }
            return computed;
        });
    }

    public static string BuildKey(string tenantId, string module, string kind, string hash)
    {
        return $"tilsoft:{tenantId}:{module}:{kind}:{hash}";
    }

    public static string ComputeHash(params string[] parts)
    {
        using var sha = SHA256.Create();
        var combined = string.Join('|', parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private bool CanCache(bool containsSensitive)
    {
        if (!Enabled)
        {
            return false;
        }

        if (containsSensitive && !_options.AllowSensitiveContent)
        {
            return false;
        }

        return true;
    }

    private static (string Digest, string QuestionHash, string ToolDigest, string PlanDigest) ComputeCacheDigests(
        TilsoftExecutionContext context,
        string question,
        IReadOnlyList<ToolDefinition> tools,
        string? planJson)
    {
        var normalizedQuestion = NormalizeQuestion(question);
        var questionHash = ComputeHash(normalizedQuestion);
        var toolDigest = ComputeToolDigest(tools);
        var planDigest = string.IsNullOrWhiteSpace(planJson) ? string.Empty : ComputeHash(planJson);

        var digest = ComputeHash(context.Language, normalizedQuestion, toolDigest, planDigest);
        return (digest, questionHash, toolDigest, planDigest);
    }

    private static string NormalizeQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return string.Empty;
        }

        return question.Trim().ToLowerInvariant();
    }

    private static string ComputeToolDigest(IReadOnlyList<ToolDefinition> tools)
    {
        if (tools.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var tool in tools.OrderBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(tool.Name).Append('|')
                .Append(tool.JsonSchema).Append('|')
                .Append(tool.Instruction).Append('|')
                .Append(tool.SpName ?? string.Empty).Append(';');
        }

        return ComputeHash(builder.ToString());
    }

    private async Task<string?> TryGetSqlCacheAsync(
        TilsoftExecutionContext context,
        string module,
        string cacheKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_sqlOptions.ConnectionString))
        {
            return null;
        }

        try
        {
            await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
            await connection.OpenAsync(ct);

            await using var command = new SqlCommand("dbo.ai_semantic_cache_get", connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = _sqlOptions.CommandTimeoutSeconds
            };

            command.Parameters.Add(new SqlParameter("@TenantId", context.TenantId));
            command.Parameters.Add(new SqlParameter("@Module", module));
            command.Parameters.Add(new SqlParameter("@CacheKey", cacheKey));

            var result = await command.ExecuteScalarAsync(ct);
            return result as string;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SQL semantic cache read failed.");
            return null;
        }
    }

    private async Task TrySetSqlCacheAsync(
        TilsoftExecutionContext context,
        string module,
        string cacheKey,
        string questionHash,
        string toolDigest,
        string planDigest,
        string answer,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_sqlOptions.ConnectionString))
        {
            return;
        }

        try
        {
            await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
            await connection.OpenAsync(ct);

            await using var command = new SqlCommand("dbo.ai_semantic_cache_put", connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = _sqlOptions.CommandTimeoutSeconds
            };

            command.Parameters.Add(new SqlParameter("@TenantId", context.TenantId));
            command.Parameters.Add(new SqlParameter("@Module", module));
            command.Parameters.Add(new SqlParameter("@CacheKey", cacheKey));
            command.Parameters.Add(new SqlParameter("@QuestionHash", questionHash));
            command.Parameters.Add(new SqlParameter("@ToolsHash", string.IsNullOrWhiteSpace(toolDigest) ? DBNull.Value : toolDigest));
            command.Parameters.Add(new SqlParameter("@PlanHash", string.IsNullOrWhiteSpace(planDigest) ? DBNull.Value : planDigest));
            command.Parameters.Add(new SqlParameter("@Answer", answer));

            await command.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SQL semantic cache write failed.");
        }
    }
}
