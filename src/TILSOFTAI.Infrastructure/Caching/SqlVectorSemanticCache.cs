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

using System.Text.Json;
using TILSOFTAI.Infrastructure.Caching;

public sealed class SqlVectorSemanticCache : ISemanticCache
{
    private readonly IEmbeddingClient _embeddingClient;
    private readonly SemanticCacheOptions _options;
    private readonly SqlOptions _sqlOptions;
    private readonly ILogger<SqlVectorSemanticCache> _logger;

    public SqlVectorSemanticCache(
        IEmbeddingClient embeddingClient,
        IOptions<SemanticCacheOptions> options,
        IOptions<SqlOptions> sqlOptions,
        ILogger<SqlVectorSemanticCache> logger)
    {
        _embeddingClient = embeddingClient ?? throw new ArgumentNullException(nameof(embeddingClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool Enabled => _options.Enabled && string.Equals(_options.Mode, "SqlVector", StringComparison.OrdinalIgnoreCase);

    public async Task<string?> TryGetAnswerAsync(
        TilsoftExecutionContext context,
        string module,
        string question,
        IReadOnlyList<ToolDefinition> tools,
        string? planJson,
        bool containsSensitive,
        CancellationToken ct)
    {
        if (!Enabled) return null;
        if (containsSensitive && !_options.AllowSensitiveContent) return null;

        try
        {
            var normalizedQuestion = NormalizeQuestion(question);
            var (digest, _, toolDigest, planDigest) = ComputeCacheDigests(context, normalizedQuestion, tools, planJson);
            
            // 1. Generate Embedding
            var embedding = await _embeddingClient.GenerateEmbeddingAsync(normalizedQuestion, ct);
            if (embedding.Length == 0) return null;

            // 2. Search in SQL
            // We verify tool digest and plan digest match exactly to ensure context safety
            return await SearchSqlAsync(context, module, normalizedQuestion, toolDigest, planDigest, embedding, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SqlVectorSemanticCache lookup failed.");
            return null;
        }
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
        if (!Enabled) return;
        if (containsSensitive && !_options.AllowSensitiveContent) return;

        try
        {
            var normalizedQuestion = NormalizeQuestion(question);
            var (digest, questionHash, toolDigest, planDigest) = ComputeCacheDigests(context, normalizedQuestion, tools, planJson);

            // 1. Generate Embedding
            var embedding = await _embeddingClient.GenerateEmbeddingAsync(normalizedQuestion, ct);
            if (embedding.Length == 0) return;

            // 2. Upsert in SQL
            await UpsertSqlAsync(context, module, questionHash, normalizedQuestion, toolDigest, planDigest, answer, embedding, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SqlVectorSemanticCache write failed.");
        }
    }

    private async Task<string?> SearchSqlAsync(
        TilsoftExecutionContext context, string module, string question, string toolHash, string planHash, float[] embedding, CancellationToken ct)
    {
        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand("dbo.app_semanticcache_search", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _sqlOptions.CommandTimeoutSeconds
        };

        command.Parameters.Add(new SqlParameter("@TenantId", context.TenantId));
        command.Parameters.Add(new SqlParameter("@Module", module));
        command.Parameters.Add(new SqlParameter("@ToolHash", (object?)toolHash ?? DBNull.Value));
        command.Parameters.Add(new SqlParameter("@PlanHash", (object?)planHash ?? DBNull.Value));
        command.Parameters.Add(new SqlParameter("@EmbeddingJson", JsonSerializer.Serialize(embedding))); // Pass as JSON, SP will handle vector logic
        command.Parameters.Add(new SqlParameter("@TopK", 1));
        command.Parameters.Add(new SqlParameter("@MinSimilarity", 0.9)); // High threshold for semantic cache

        var result = await command.ExecuteScalarAsync(ct);
        return result as string;
    }

    private async Task UpsertSqlAsync(
        TilsoftExecutionContext context, string module, string questionHash, string questionText, 
        string toolHash, string planHash, string answer, float[] embedding, CancellationToken ct)
    {
        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand("dbo.app_semanticcache_upsert", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _sqlOptions.CommandTimeoutSeconds
        };

        command.Parameters.Add(new SqlParameter("@TenantId", context.TenantId));
        command.Parameters.Add(new SqlParameter("@Module", module));
        command.Parameters.Add(new SqlParameter("@QuestionHash", questionHash));
        command.Parameters.Add(new SqlParameter("@QuestionText", questionText));
        command.Parameters.Add(new SqlParameter("@ToolHash", (object?)toolHash ?? DBNull.Value));
        command.Parameters.Add(new SqlParameter("@PlanHash", (object?)planHash ?? DBNull.Value));
        command.Parameters.Add(new SqlParameter("@Answer", answer));
        command.Parameters.Add(new SqlParameter("@EmbeddingJson", JsonSerializer.Serialize(embedding)));

        await command.ExecuteNonQueryAsync(ct);
    }

    // --- Helper Logic (Duplicated from SemanticCache.cs) ---

     private static (string Digest, string QuestionHash, string ToolDigest, string PlanDigest) ComputeCacheDigests(
        TilsoftExecutionContext context,
        string normalizedQuestion,
        IReadOnlyList<ToolDefinition> tools,
        string? planJson)
    {
        var questionHash = ComputeHash(normalizedQuestion);
        var toolDigest = ComputeToolDigest(tools);
        var planDigest = string.IsNullOrWhiteSpace(planJson) ? string.Empty : ComputeHash(planJson);

        var digest = ComputeHash(context.Language, normalizedQuestion, toolDigest, planDigest);
        return (digest, questionHash, toolDigest, planDigest);
    }

    private static string NormalizeQuestion(string question)
    {
        return string.IsNullOrWhiteSpace(question) ? string.Empty : question.Trim().ToLowerInvariant();
    }

    private static string ComputeToolDigest(IReadOnlyList<ToolDefinition> tools)
    {
        if (tools.Count == 0) return string.Empty;

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

    private static string ComputeHash(params string[] parts)
    {
        using var sha = SHA256.Create();
        var combined = string.Join('|', parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
