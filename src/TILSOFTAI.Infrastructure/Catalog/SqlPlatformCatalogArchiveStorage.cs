using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed class SqlPlatformCatalogArchiveStorage : IPlatformCatalogArchiveStorage
{
    private readonly SqlOptions _sqlOptions;

    public SqlPlatformCatalogArchiveStorage(IOptions<SqlOptions> sqlOptions)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
    }

    public string BackendName => "managed-sql";
    public string BackendClass => "managed_durable";
    public string RetentionPosture => "retention_tracked";
    public bool ImmutabilityEnforced => true;

    public async Task<CatalogArchiveStorageWriteResult> WriteAsync(
        string manifestId,
        string content,
        CancellationToken ct)
    {
        var hash = Sha256(content);
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_archive_upsert");
        command.Parameters.Add(new SqlParameter("@ManifestId", SqlDbType.NVarChar, 64) { Value = manifestId });
        command.Parameters.Add(new SqlParameter("@ArchiveJson", SqlDbType.NVarChar, -1) { Value = content });
        command.Parameters.Add(new SqlParameter("@ArchiveHash", SqlDbType.NVarChar, 128) { Value = hash });
        command.Parameters.Add(new SqlParameter("@BackendClass", SqlDbType.NVarChar, 80) { Value = BackendClass });
        command.Parameters.Add(new SqlParameter("@RetentionPosture", SqlDbType.NVarChar, 80) { Value = RetentionPosture });
        command.Parameters.Add(new SqlParameter("@ImmutabilityEnforced", SqlDbType.Bit) { Value = ImmutabilityEnforced });
        await command.ExecuteNonQueryAsync(ct);

        return new CatalogArchiveStorageWriteResult
        {
            BackendName = BackendName,
            BackendClass = BackendClass,
            RetentionPosture = RetentionPosture,
            ImmutabilityEnforced = ImmutabilityEnforced,
            ArchivePath = $"sql://PlatformCatalogDossierArchive/{manifestId}",
            StorageUri = $"archive://managed-sql/{manifestId}",
            RecoveryState = "managed_sql_written"
        };
    }

    public async Task<CatalogArchiveStorageReadResult> ReadAsync(
        string manifestId,
        CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_archive_get");
        command.Parameters.Add(new SqlParameter("@ManifestId", SqlDbType.NVarChar, 64) { Value = manifestId });
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return new CatalogArchiveStorageReadResult
            {
                Found = false,
                BackendName = BackendName,
                BackendClass = BackendClass,
                RetentionPosture = RetentionPosture,
                ImmutabilityEnforced = ImmutabilityEnforced,
                ArchivePath = $"sql://PlatformCatalogDossierArchive/{manifestId}",
                StorageUri = $"archive://managed-sql/{manifestId}",
                RecoveryState = "managed_sql_missing"
            };
        }

        return new CatalogArchiveStorageReadResult
        {
            Found = true,
            BackendName = BackendName,
            BackendClass = reader["BackendClass"] as string ?? BackendClass,
            RetentionPosture = reader["RetentionPosture"] as string ?? RetentionPosture,
            ImmutabilityEnforced = reader["ImmutabilityEnforced"] != DBNull.Value && (bool)reader["ImmutabilityEnforced"],
            ArchivePath = $"sql://PlatformCatalogDossierArchive/{manifestId}",
            StorageUri = $"archive://managed-sql/{manifestId}",
            RecoveryState = "managed_sql_read",
            Content = reader["ArchiveJson"] as string ?? string.Empty
        };
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

    private static string Sha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
}
