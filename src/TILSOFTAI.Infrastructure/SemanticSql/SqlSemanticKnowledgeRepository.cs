using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Infrastructure.SemanticSql;

public sealed class SqlSemanticKnowledgeRepository : ISemanticKnowledgeRepository
{
    private readonly SqlOptions _sqlOptions;

    public SqlSemanticKnowledgeRepository(IOptions<SqlOptions> sqlOptions)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
    }

    public async Task<IReadOnlyList<KnowledgeChunk>> SearchChunksAsync(
        SemanticSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var candidates = new List<KnowledgeChunk>();
        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandTimeout = _sqlOptions.CommandTimeoutSeconds;
        command.CommandText = @"
SELECT TOP (@CandidateLimit)
    ChunkID,
    TenantID,
    ChunkType,
    ObjectKey,
    Domain,
    Locale,
    Title,
    ContentText,
    Metadata
FROM ai.KnowledgeChunk
WHERE IsActive = 1
  AND (TenantID IS NULL OR TenantID = @TenantID)
  AND (@Locale IS NULL OR Locale IS NULL OR Locale = @Locale)
  AND (@DomainCount = 0 OR Domain IN (SELECT [value] FROM OPENJSON(@DomainsJson)))
ORDER BY UpdatedAt DESC, ChunkID DESC;";

        command.Parameters.Add(new SqlParameter("@CandidateLimit", SqlDbType.Int) { Value = Math.Max(request.TopK * 8, 50) });
        command.Parameters.Add(new SqlParameter("@TenantID", SqlDbType.NVarChar, 100) { Value = DbValue(request.TenantId) });
        command.Parameters.Add(new SqlParameter("@Locale", SqlDbType.NVarChar, 20) { Value = DbValue(request.Locale) });
        command.Parameters.Add(new SqlParameter("@DomainCount", SqlDbType.Int) { Value = request.Domains.Count });
        command.Parameters.Add(new SqlParameter("@DomainsJson", SqlDbType.NVarChar, -1) { Value = JsonSerializer.Serialize(request.Domains) });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var searchable = string.Join(' ',
                ReadString(reader, "ObjectKey"),
                ReadString(reader, "Domain"),
                ReadString(reader, "Title"),
                ReadString(reader, "ContentText"),
                ReadString(reader, "Metadata"));

            var score = SemanticScoring.ScoreText(
                request.Text,
                searchable,
                ReadString(reader, "Domain"),
                request.Domains,
                request.HardSignals);

            candidates.Add(new KnowledgeChunk
            {
                ChunkId = ReadInt64(reader, "ChunkID"),
                TenantId = ReadString(reader, "TenantID"),
                ChunkType = ReadString(reader, "ChunkType") ?? string.Empty,
                ObjectKey = ReadString(reader, "ObjectKey") ?? string.Empty,
                Domain = ReadString(reader, "Domain"),
                Locale = ReadString(reader, "Locale"),
                Title = ReadString(reader, "Title"),
                ContentText = ReadString(reader, "ContentText") ?? string.Empty,
                Metadata = ReadString(reader, "Metadata"),
                Score = score
            });
        }

        return candidates
            .OrderByDescending(chunk => chunk.Score)
            .ThenBy(chunk => chunk.ObjectKey, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(request.TopK, 1))
            .ToArray();
    }

    private static object DbValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

    private static string? ReadString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static long ReadInt64(SqlDataReader reader, string name) =>
        reader.GetInt64(reader.GetOrdinal(name));
}
