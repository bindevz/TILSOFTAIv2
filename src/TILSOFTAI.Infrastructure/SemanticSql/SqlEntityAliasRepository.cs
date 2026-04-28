using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Infrastructure.SemanticSql;

public sealed class SqlEntityAliasRepository : IEntityAliasRepository
{
    private readonly SqlOptions _sqlOptions;

    public SqlEntityAliasRepository(IOptions<SqlOptions> sqlOptions)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
    }

    public async Task<IReadOnlyList<EntityCandidate>> SearchAliasesAsync(
        EntityAliasSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedText = SemanticScoring.Normalize(request.Text);
        var candidates = new List<EntityCandidate>();

        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandTimeout = _sqlOptions.CommandTimeoutSeconds;
        command.CommandText = @"
SELECT TOP (@CandidateLimit)
    EntityAliasID,
    TenantID,
    EntityType,
    EntityID,
    CanonicalCode,
    CanonicalName,
    AliasText,
    AliasNormalized,
    Locale,
    Metadata
FROM ai.EntityAlias
WHERE IsActive = 1
  AND (TenantID IS NULL OR TenantID = @TenantID)
  AND (@Locale IS NULL OR Locale IS NULL OR Locale = @Locale)
  AND (@EntityTypeCount = 0 OR EntityType IN (SELECT [value] FROM OPENJSON(@EntityTypesJson)))
  AND (
      @SearchText = N''
      OR AliasNormalized LIKE N'%' + @SearchText + N'%'
      OR @SearchText LIKE N'%' + AliasNormalized + N'%'
      OR CanonicalCode LIKE N'%' + @SearchText + N'%'
      OR CanonicalName LIKE N'%' + @SearchText + N'%'
  )
ORDER BY UpdatedAt DESC, EntityAliasID DESC;";

        command.Parameters.Add(new SqlParameter("@CandidateLimit", SqlDbType.Int) { Value = Math.Max(request.TopK * 8, 50) });
        command.Parameters.Add(new SqlParameter("@TenantID", SqlDbType.NVarChar, 100) { Value = DbValue(request.TenantId) });
        command.Parameters.Add(new SqlParameter("@Locale", SqlDbType.NVarChar, 20) { Value = DbValue(request.Locale) });
        command.Parameters.Add(new SqlParameter("@EntityTypeCount", SqlDbType.Int) { Value = request.EntityTypes.Count });
        command.Parameters.Add(new SqlParameter("@EntityTypesJson", SqlDbType.NVarChar, -1) { Value = JsonSerializer.Serialize(request.EntityTypes) });
        command.Parameters.Add(new SqlParameter("@SearchText", SqlDbType.NVarChar, 300) { Value = normalizedText });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var searchable = string.Join(' ',
                ReadString(reader, "EntityType"),
                ReadString(reader, "EntityID"),
                ReadString(reader, "CanonicalCode"),
                ReadString(reader, "CanonicalName"),
                ReadString(reader, "AliasText"),
                ReadString(reader, "AliasNormalized"),
                ReadString(reader, "Metadata"));

            candidates.Add(new EntityCandidate
            {
                EntityAliasId = ReadInt64(reader, "EntityAliasID"),
                TenantId = ReadString(reader, "TenantID"),
                EntityType = ReadString(reader, "EntityType") ?? string.Empty,
                EntityId = ReadString(reader, "EntityID") ?? string.Empty,
                CanonicalCode = ReadString(reader, "CanonicalCode"),
                CanonicalName = ReadString(reader, "CanonicalName"),
                AliasText = ReadString(reader, "AliasText") ?? string.Empty,
                AliasNormalized = ReadString(reader, "AliasNormalized") ?? string.Empty,
                Locale = ReadString(reader, "Locale"),
                Metadata = ReadString(reader, "Metadata"),
                Score = SemanticScoring.ScoreText(
                    request.Text,
                    searchable,
                    domain: null,
                    requestedDomains: Array.Empty<string>(),
                    hardSignals: request.EntityTypes)
            });
        }

        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.EntityType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.CanonicalCode, StringComparer.OrdinalIgnoreCase)
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
