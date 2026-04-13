using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed class SqlPlatformCatalogPromotionManifestStore : IPlatformCatalogPromotionManifestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SqlOptions _sqlOptions;

    public SqlPlatformCatalogPromotionManifestStore(IOptions<SqlOptions> sqlOptions)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
    }

    public async Task<CatalogPromotionManifestRecord> CreateManifestAsync(
        CatalogPromotionManifestRecord manifest,
        CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_promotionmanifest_create");
        command.Parameters.Add(new SqlParameter("@ManifestId", SqlDbType.NVarChar, 64) { Value = manifest.ManifestId });
        command.Parameters.Add(new SqlParameter("@ManifestHash", SqlDbType.NVarChar, 128) { Value = manifest.ManifestHash });
        command.Parameters.Add(new SqlParameter("@EnvironmentName", SqlDbType.NVarChar, 100) { Value = manifest.EnvironmentName });
        command.Parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 50) { Value = manifest.Status });
        command.Parameters.Add(new SqlParameter("@ChangeIdsJson", SqlDbType.NVarChar, -1) { Value = ToJson(manifest.ChangeIds) });
        command.Parameters.Add(new SqlParameter("@EvidenceIdsJson", SqlDbType.NVarChar, -1) { Value = ToJson(manifest.EvidenceIds) });
        command.Parameters.Add(new SqlParameter("@GateSummaryJson", SqlDbType.NVarChar, -1) { Value = manifest.GateSummaryJson });
        command.Parameters.Add(new SqlParameter("@RollbackOfManifestId", SqlDbType.NVarChar, 64) { Value = DbNullable(manifest.RollbackOfManifestId) });
        command.Parameters.Add(new SqlParameter("@RelatedIncidentId", SqlDbType.NVarChar, 100) { Value = DbNullable(manifest.RelatedIncidentId) });
        command.Parameters.Add(new SqlParameter("@Notes", SqlDbType.NVarChar, 1000) { Value = DbNullable(manifest.Notes) });
        command.Parameters.Add(new SqlParameter("@CreatedByUserId", SqlDbType.NVarChar, 200) { Value = manifest.CreatedByUserId });
        command.Parameters.Add(new SqlParameter("@IssuedByUserId", SqlDbType.NVarChar, 200) { Value = manifest.IssuedByUserId });
        command.Parameters.Add(new SqlParameter("@CorrelationId", SqlDbType.NVarChar, 100) { Value = DbNullable(manifest.CorrelationId) });
        command.Parameters.Add(new SqlParameter("@CreatedAtUtc", SqlDbType.DateTime2) { Value = manifest.CreatedAtUtc });
        command.Parameters.Add(new SqlParameter("@IssuedAtUtc", SqlDbType.DateTime2) { Value = manifest.IssuedAtUtc });
        await using var reader = await command.ExecuteReaderAsync(ct);

        return await reader.ReadAsync(ct)
            ? ReadManifest(reader)
            : throw new InvalidOperationException("Failed to create platform catalog promotion manifest.");
    }

    public async Task<CatalogPromotionManifestRecord?> GetManifestAsync(string manifestId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_promotionmanifest_get");
        command.Parameters.Add(new SqlParameter("@ManifestId", SqlDbType.NVarChar, 64) { Value = manifestId });
        await using var reader = await command.ExecuteReaderAsync(ct);

        return await reader.ReadAsync(ct) ? ReadManifest(reader) : null;
    }

    public async Task<IReadOnlyList<CatalogPromotionManifestRecord>> ListManifestsAsync(
        string environmentName,
        CancellationToken ct)
    {
        var records = new List<CatalogPromotionManifestRecord>();
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_promotionmanifest_list");
        command.Parameters.Add(new SqlParameter("@EnvironmentName", SqlDbType.NVarChar, 100) { Value = environmentName });
        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            records.Add(ReadManifest(reader));
        }

        return records;
    }

    public async Task<CatalogRolloutAttestationRecord> CreateAttestationAsync(
        CatalogRolloutAttestationRecord attestation,
        CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_promotionattestation_create");
        command.Parameters.Add(new SqlParameter("@AttestationId", SqlDbType.NVarChar, 64) { Value = attestation.AttestationId });
        command.Parameters.Add(new SqlParameter("@ManifestId", SqlDbType.NVarChar, 64) { Value = attestation.ManifestId });
        command.Parameters.Add(new SqlParameter("@EnvironmentName", SqlDbType.NVarChar, 100) { Value = attestation.EnvironmentName });
        command.Parameters.Add(new SqlParameter("@State", SqlDbType.NVarChar, 50) { Value = attestation.State });
        command.Parameters.Add(new SqlParameter("@Notes", SqlDbType.NVarChar, 1000) { Value = DbNullable(attestation.Notes) });
        command.Parameters.Add(new SqlParameter("@EvidenceIdsJson", SqlDbType.NVarChar, -1) { Value = ToJson(attestation.EvidenceIds) });
        command.Parameters.Add(new SqlParameter("@ActorUserId", SqlDbType.NVarChar, 200) { Value = attestation.ActorUserId });
        command.Parameters.Add(new SqlParameter("@AcceptedByUserId", SqlDbType.NVarChar, 200) { Value = DbNullable(attestation.AcceptedByUserId) });
        command.Parameters.Add(new SqlParameter("@CorrelationId", SqlDbType.NVarChar, 100) { Value = DbNullable(attestation.CorrelationId) });
        command.Parameters.Add(new SqlParameter("@CreatedAtUtc", SqlDbType.DateTime2) { Value = attestation.CreatedAtUtc });
        await using var reader = await command.ExecuteReaderAsync(ct);

        return await reader.ReadAsync(ct)
            ? ReadAttestation(reader)
            : throw new InvalidOperationException("Failed to create platform catalog rollout attestation.");
    }

    public async Task<IReadOnlyList<CatalogRolloutAttestationRecord>> ListAttestationsAsync(
        string manifestId,
        CancellationToken ct)
    {
        var records = new List<CatalogRolloutAttestationRecord>();
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_promotionattestation_list");
        command.Parameters.Add(new SqlParameter("@ManifestId", SqlDbType.NVarChar, 64) { Value = manifestId });
        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            records.Add(ReadAttestation(reader));
        }

        return records;
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

    private static CatalogPromotionManifestRecord ReadManifest(SqlDataReader reader) => new()
    {
        ManifestId = ReaderString(reader, "ManifestId"),
        ManifestHash = ReaderString(reader, "ManifestHash"),
        EnvironmentName = ReaderString(reader, "EnvironmentName"),
        Status = ReaderString(reader, "Status"),
        ChangeIds = FromJson(ReaderString(reader, "ChangeIdsJson")),
        EvidenceIds = FromJson(ReaderString(reader, "EvidenceIdsJson")),
        GateSummaryJson = ReaderString(reader, "GateSummaryJson"),
        RollbackOfManifestId = ReaderString(reader, "RollbackOfManifestId"),
        RelatedIncidentId = ReaderString(reader, "RelatedIncidentId"),
        Notes = ReaderString(reader, "Notes"),
        CreatedByUserId = ReaderString(reader, "CreatedByUserId"),
        IssuedByUserId = ReaderString(reader, "IssuedByUserId"),
        CorrelationId = ReaderString(reader, "CorrelationId"),
        CreatedAtUtc = ReaderDateTime(reader, "CreatedAtUtc") ?? DateTime.MinValue,
        IssuedAtUtc = ReaderDateTime(reader, "IssuedAtUtc") ?? DateTime.MinValue
    };

    private static CatalogRolloutAttestationRecord ReadAttestation(SqlDataReader reader) => new()
    {
        AttestationId = ReaderString(reader, "AttestationId"),
        ManifestId = ReaderString(reader, "ManifestId"),
        EnvironmentName = ReaderString(reader, "EnvironmentName"),
        State = ReaderString(reader, "State"),
        Notes = ReaderString(reader, "Notes"),
        EvidenceIds = FromJson(ReaderString(reader, "EvidenceIdsJson")),
        ActorUserId = ReaderString(reader, "ActorUserId"),
        AcceptedByUserId = ReaderString(reader, "AcceptedByUserId"),
        CorrelationId = ReaderString(reader, "CorrelationId"),
        CreatedAtUtc = ReaderDateTime(reader, "CreatedAtUtc") ?? DateTime.MinValue
    };

    private static string ToJson(IReadOnlyList<string> values) => JsonSerializer.Serialize(values, JsonOptions);

    private static IReadOnlyList<string> FromJson(string json) =>
        string.IsNullOrWhiteSpace(json)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? Array.Empty<string>();

    private static object DbNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

    private static string ReaderString(SqlDataReader reader, string name) =>
        reader[name] != DBNull.Value ? reader[name] as string ?? string.Empty : string.Empty;

    private static DateTime? ReaderDateTime(SqlDataReader reader, string name) =>
        reader[name] != DBNull.Value ? (DateTime)reader[name] : null;
}
