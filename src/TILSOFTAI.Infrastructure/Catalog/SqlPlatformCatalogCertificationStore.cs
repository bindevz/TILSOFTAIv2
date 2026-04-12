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
        await using var reader = await command.ExecuteReaderAsync(ct);

        return await reader.ReadAsync(ct)
            ? ReadEvidence(reader)
            : throw new InvalidOperationException("Failed to create platform catalog certification evidence.");
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
        CapturedAtUtc = reader["CapturedAtUtc"] != DBNull.Value ? (DateTime)reader["CapturedAtUtc"] : DateTime.MinValue
    };

    private static object DbNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
}
