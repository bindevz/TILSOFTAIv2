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
    private readonly SensitiveDataOptions _sensitiveDataOptions;
    private readonly SqlOptions _sqlOptions;
    private readonly ILogger<SqlVectorSemanticCache> _logger;

    public SqlVectorSemanticCache(
        IEmbeddingClient embeddingClient,
        IOptions<SemanticCacheOptions> options,
        IOptions<SensitiveDataOptions> sensitiveDataOptions,
        IOptions<SqlOptions> sqlOptions,
        ILogger<SqlVectorSemanticCache> logger)
    {
        _embeddingClient = embeddingClient ?? throw new ArgumentNullException(nameof(embeddingClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _sensitiveDataOptions = sensitiveDataOptions?.Value ?? throw new ArgumentNullException(nameof(sensitiveDataOptions));
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
        if (containsSensitive && _sensitiveDataOptions.DisableCachingWhenSensitive) return null;
        if (containsSensitive && !_options.AllowSensitiveContent) return null;

        try
        {
            var normalizedQuestion = NormalizeQuestion(question);
            var (digest, _, toolDigest, planDigest) = ComputeCacheDigests(context, normalizedQuestion, tools, planJson);
            
            // 1. Generate Embedding (SQL or C#)
            float[] embedding;
            if (_options.UseSqlEmbeddings)
            {
                _logger.LogDebug("Attempting to use SQL Server AI embeddings.");
                embedding = await GenerateEmbeddingSqlAsync(normalizedQuestion, ct);
                
                // Fallback to C# if SQL returns null/empty
                if (embedding == null || embedding.Length == 0)
                {
                    _logger.LogWarning("SQL embedding failed or unavailable, falling back to C# embedding client.");
                    embedding = await _embeddingClient.GenerateEmbeddingAsync(normalizedQuestion, ct);
                }
            }
            else
            {
                embedding = await _embeddingClient.GenerateEmbeddingAsync(normalizedQuestion, ct);
            }
            
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
        if (containsSensitive && _sensitiveDataOptions.DisableCachingWhenSensitive) return;
        if (containsSensitive && !_options.AllowSensitiveContent) return;

        try
        {
            var normalizedQuestion = NormalizeQuestion(question);
            var (digest, questionHash, toolDigest, planDigest) = ComputeCacheDigests(context, normalizedQuestion, tools, planJson);

            // 1. Generate Embedding (SQL or C#)
            float[] embedding;
            if (_options.UseSqlEmbeddings)
            {
                _logger.LogDebug("Attempting to use SQL Server AI embeddings for cache write.");
                embedding = await GenerateEmbeddingSqlAsync(normalizedQuestion, ct);
                
                // Fallback to C# if SQL returns null/empty
                if (embedding == null || embedding.Length == 0)
                {
                    _logger.LogWarning("SQL embedding failed or unavailable for cache write, falling back to C# embedding client.");
                    embedding = await _embeddingClient.GenerateEmbeddingAsync(normalizedQuestion, ct);
                }
            }
            else
            {
                embedding = await _embeddingClient.GenerateEmbeddingAsync(normalizedQuestion, ct);
            }
            
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

    private async Task<float[]?> GenerateEmbeddingSqlAsync(string text, CancellationToken ct)
    {
        try
        {
            await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
            await connection.OpenAsync(ct);

            await using var command = new SqlCommand("dbo.app_semanticcache_embed", connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = _sqlOptions.CommandTimeoutSeconds
            };

            command.Parameters.Add(new SqlParameter("@ModelName", _options.SqlEmbeddingModelName));
            command.Parameters.Add(new SqlParameter("@Text", text));

            var result = await command.ExecuteScalarAsync(ct);
            if (result == null || result == DBNull.Value)
            {
                _logger.LogWarning("SQL embedding procedure returned NULL. AI_GENERATE_EMBEDDINGS may not be available or EXTERNAL MODEL not configured.");
                return null;
            }

            var embeddingJson = result.ToString();
            if (string.IsNullOrWhiteSpace(embeddingJson))
            {
                _logger.LogWarning("SQL embedding procedure returned empty string.");
                return null;
            }

            // Parse JSON array to float[]
            var embedding = JsonSerializer.Deserialize<float[]>(embeddingJson);
            if (embedding == null || embedding.Length == 0)
            {
                _logger.LogWarning("SQL embedding JSON deserialization failed or returned empty array.");
                return null;
            }

            _logger.LogDebug("Successfully generated embedding using SQL Server AI (dimension: {Dimension}).", embedding.Length);
            return embedding;
        }
        catch (SqlException ex)
        {
            // Common errors: procedure not found, EXTERNAL MODEL missing, permissions
            _logger.LogError(ex, "SQL error generating embedding. Procedure may not exist or AI function unavailable.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating SQL embedding.");
            return null;
        }
    }
}
