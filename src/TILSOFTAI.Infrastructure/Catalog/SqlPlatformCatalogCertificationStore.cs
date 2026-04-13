using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed class SqlPlatformCatalogCertificationStore : IPlatformCatalogCertificationStore
{
    private readonly SqlOptions _sqlOptions;

    public SqlPlatformCatalogCertificationStore(IOptions<SqlOptions> sqlOptions)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
    }

    public async Task<IReadOnlyList<CatalogCertificationEvidenceRecord>> ListEvidenceAsync(
        string environmentName,
        CancellationToken ct)
    {
        var records = new List<CatalogCertificationEvidenceRecord>();
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_catalogcertification_list");
        command.Parameters.Add(new SqlParameter("@EnvironmentName", SqlDbType.NVarChar, 100) { Value = environmentName });
        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            records.Add(ReadEvidence(reader));
        }

        return records;
    }

    public async Task<CatalogCertificationEvidenceRecord?> GetEvidenceAsync(
        string evidenceId,
        CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_catalogcertification_get");
        command.Parameters.Add(new SqlParameter("@EvidenceId", SqlDbType.NVarChar, 64) { Value = evidenceId });
        await using var reader = await command.ExecuteReaderAsync(ct);

        return await reader.ReadAsync(ct) ? ReadEvidence(reader) : null;
    }

    public async Task<CatalogCertificationEvidenceRecord> CreateEvidenceAsync(
        CatalogCertificationEvidenceRecord evidence,
        CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_catalogcertification_create");
        command.Parameters.Add(new SqlParameter("@EvidenceId", SqlDbType.NVarChar, 64) { Value = evidence.EvidenceId });
        command.Parameters.Add(new SqlParameter("@EnvironmentName", SqlDbType.NVarChar, 100) { Value = evidence.EnvironmentName });
        command.Parameters.Add(new SqlParameter("@EvidenceKind", SqlDbType.NVarChar, 100) { Value = evidence.EvidenceKind });
        command.Parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 50) { Value = evidence.Status });
        command.Parameters.Add(new SqlParameter("@Summary", SqlDbType.NVarChar, 1000) { Value = evidence.Summary });
        command.Parameters.Add(new SqlParameter("@EvidenceUri", SqlDbType.NVarChar, 1000) { Value = DbNullable(evidence.EvidenceUri) });
        command.Parameters.Add(new SqlParameter("@RelatedChangeId", SqlDbType.NVarChar, 64) { Value = DbNullable(evidence.RelatedChangeId) });
        command.Parameters.Add(new SqlParameter("@RelatedIncidentId", SqlDbType.NVarChar, 100) { Value = DbNullable(evidence.RelatedIncidentId) });
        command.Parameters.Add(new SqlParameter("@OperatorUserId", SqlDbType.NVarChar, 200) { Value = evidence.OperatorUserId });
        command.Parameters.Add(new SqlParameter("@ApprovedByUserId", SqlDbType.NVarChar, 200) { Value = DbNullable(evidence.ApprovedByUserId) });
        command.Parameters.Add(new SqlParameter("@CorrelationId", SqlDbType.NVarChar, 100) { Value = DbNullable(evidence.CorrelationId) });
        command.Parameters.Add(new SqlParameter("@ArtifactHash", SqlDbType.NVarChar, 128) { Value = DbNullable(evidence.ArtifactHash) });
        command.Parameters.Add(new SqlParameter("@ArtifactHashAlgorithm", SqlDbType.NVarChar, 40) { Value = DbNullable(evidence.ArtifactHashAlgorithm) });
        command.Parameters.Add(new SqlParameter("@ArtifactContentType", SqlDbType.NVarChar, 200) { Value = DbNullable(evidence.ArtifactContentType) });
        command.Parameters.Add(new SqlParameter("@ArtifactType", SqlDbType.NVarChar, 100) { Value = DbNullable(evidence.ArtifactType) });
        command.Parameters.Add(new SqlParameter("@SourceSystem", SqlDbType.NVarChar, 100) { Value = DbNullable(evidence.SourceSystem) });
        command.Parameters.Add(new SqlParameter("@CollectedAtUtc", SqlDbType.DateTime2) { Value = DbNullable(evidence.CollectedAtUtc) });
        command.Parameters.Add(new SqlParameter("@VerificationStatus", SqlDbType.NVarChar, 50) { Value = evidence.VerificationStatus });
        command.Parameters.Add(new SqlParameter("@VerificationNotes", SqlDbType.NVarChar, 1000) { Value = DbNullable(evidence.VerificationNotes) });
        command.Parameters.Add(new SqlParameter("@VerifiedByUserId", SqlDbType.NVarChar, 200) { Value = DbNullable(evidence.VerifiedByUserId) });
        command.Parameters.Add(new SqlParameter("@VerifiedAtUtc", SqlDbType.DateTime2) { Value = DbNullable(evidence.VerifiedAtUtc) });
        command.Parameters.Add(new SqlParameter("@ExpiresAtUtc", SqlDbType.DateTime2) { Value = DbNullable(evidence.ExpiresAtUtc) });
        command.Parameters.Add(new SqlParameter("@SupersededByEvidenceId", SqlDbType.NVarChar, 64) { Value = DbNullable(evidence.SupersededByEvidenceId) });
        command.Parameters.Add(new SqlParameter("@TrustTier", SqlDbType.NVarChar, 80) { Value = DbNullable(evidence.TrustTier) });
        command.Parameters.Add(new SqlParameter("@ArtifactProvider", SqlDbType.NVarChar, 100) { Value = DbNullable(evidence.ArtifactProvider) });
        command.Parameters.Add(new SqlParameter("@ProviderVerifiedAtUtc", SqlDbType.DateTime2) { Value = DbNullable(evidence.ProviderVerifiedAtUtc) });
        command.Parameters.Add(new SqlParameter("@ArtifactSizeBytes", SqlDbType.BigInt) { Value = DbNullable(evidence.ArtifactSizeBytes) });
        await using var reader = await command.ExecuteReaderAsync(ct);

        return await reader.ReadAsync(ct)
            ? ReadEvidence(reader)
            : throw new InvalidOperationException("Failed to create platform catalog certification evidence.");
    }

    public async Task<CatalogCertificationEvidenceRecord> UpdateEvidenceVerificationAsync(
        string evidenceId,
        CatalogEvidenceVerificationResult result,
        CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_catalogcertification_verify");
        command.Parameters.Add(new SqlParameter("@EvidenceId", SqlDbType.NVarChar, 64) { Value = evidenceId });
        command.Parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 50) { Value = result.Status });
        command.Parameters.Add(new SqlParameter("@VerificationStatus", SqlDbType.NVarChar, 50) { Value = result.VerificationStatus });
        command.Parameters.Add(new SqlParameter("@VerificationNotes", SqlDbType.NVarChar, 1000) { Value = DbNullable(result.VerificationNotes) });
        command.Parameters.Add(new SqlParameter("@VerifiedByUserId", SqlDbType.NVarChar, 200) { Value = result.VerifiedByUserId });
        command.Parameters.Add(new SqlParameter("@VerifiedAtUtc", SqlDbType.DateTime2) { Value = result.VerifiedAtUtc });
        command.Parameters.Add(new SqlParameter("@ExpiresAtUtc", SqlDbType.DateTime2) { Value = DbNullable(result.ExpiresAtUtc) });
        command.Parameters.Add(new SqlParameter("@TrustTier", SqlDbType.NVarChar, 80) { Value = DbNullable(result.TrustTier) });
        command.Parameters.Add(new SqlParameter("@ArtifactProvider", SqlDbType.NVarChar, 100) { Value = DbNullable(result.ArtifactProvider) });
        command.Parameters.Add(new SqlParameter("@ProviderVerifiedAtUtc", SqlDbType.DateTime2) { Value = DbNullable(result.ProviderVerifiedAtUtc) });
        command.Parameters.Add(new SqlParameter("@ArtifactSizeBytes", SqlDbType.BigInt) { Value = DbNullable(result.ArtifactSizeBytes) });
        await using var reader = await command.ExecuteReaderAsync(ct);

        return await reader.ReadAsync(ct)
            ? ReadEvidence(reader)
            : throw new InvalidOperationException("Failed to verify platform catalog certification evidence.");
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private SqlCommand CreateCommand(SqlConnection connection, string storedProcedure) => new(storedProcedure, connection)
    {
        CommandType = CommandType.StoredProcedure,
        CommandTimeout = _sqlOptions.CommandTimeoutSeconds
    };

    private static CatalogCertificationEvidenceRecord ReadEvidence(SqlDataReader reader) => new()
    {
        EvidenceId = reader["EvidenceId"] as string ?? string.Empty,
        EnvironmentName = reader["EnvironmentName"] as string ?? string.Empty,
        EvidenceKind = reader["EvidenceKind"] as string ?? string.Empty,
        Status = reader["Status"] as string ?? string.Empty,
        Summary = reader["Summary"] as string ?? string.Empty,
        EvidenceUri = reader["EvidenceUri"] as string ?? string.Empty,
        RelatedChangeId = reader["RelatedChangeId"] as string ?? string.Empty,
        RelatedIncidentId = reader["RelatedIncidentId"] as string ?? string.Empty,
        OperatorUserId = reader["OperatorUserId"] as string ?? string.Empty,
        ApprovedByUserId = reader["ApprovedByUserId"] as string ?? string.Empty,
        CorrelationId = reader["CorrelationId"] as string ?? string.Empty,
        CapturedAtUtc = reader["CapturedAtUtc"] != DBNull.Value ? (DateTime)reader["CapturedAtUtc"] : DateTime.MinValue,
        ArtifactHash = ReaderString(reader, "ArtifactHash"),
        ArtifactHashAlgorithm = ReaderString(reader, "ArtifactHashAlgorithm"),
        ArtifactContentType = ReaderString(reader, "ArtifactContentType"),
        ArtifactType = ReaderString(reader, "ArtifactType"),
        SourceSystem = ReaderString(reader, "SourceSystem"),
        CollectedAtUtc = ReaderDateTime(reader, "CollectedAtUtc"),
        VerificationStatus = ReaderString(reader, "VerificationStatus"),
        VerificationNotes = ReaderString(reader, "VerificationNotes"),
        VerifiedByUserId = ReaderString(reader, "VerifiedByUserId"),
        VerifiedAtUtc = ReaderDateTime(reader, "VerifiedAtUtc"),
        ExpiresAtUtc = ReaderDateTime(reader, "ExpiresAtUtc"),
        SupersededByEvidenceId = ReaderString(reader, "SupersededByEvidenceId"),
        TrustTier = ReaderString(reader, "TrustTier"),
        ArtifactProvider = ReaderString(reader, "ArtifactProvider"),
        ProviderVerifiedAtUtc = ReaderDateTime(reader, "ProviderVerifiedAtUtc"),
        ArtifactSizeBytes = ReaderLong(reader, "ArtifactSizeBytes")
    };

    private static object DbNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

    private static object DbNullable(DateTime? value) =>
        value.HasValue ? value.Value : DBNull.Value;

    private static object DbNullable(long? value) =>
        value.HasValue ? value.Value : DBNull.Value;

    private static string ReaderString(SqlDataReader reader, string name) =>
        HasColumn(reader, name) && reader[name] != DBNull.Value ? reader[name] as string ?? string.Empty : string.Empty;

    private static DateTime? ReaderDateTime(SqlDataReader reader, string name) =>
        HasColumn(reader, name) && reader[name] != DBNull.Value ? (DateTime)reader[name] : null;

    private static long? ReaderLong(SqlDataReader reader, string name) =>
        HasColumn(reader, name) && reader[name] != DBNull.Value ? Convert.ToInt64(reader[name]) : null;

    private static bool HasColumn(SqlDataReader reader, string name)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
