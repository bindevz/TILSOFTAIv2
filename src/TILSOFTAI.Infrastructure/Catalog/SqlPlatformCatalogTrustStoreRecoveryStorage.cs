using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed class SqlPlatformCatalogTrustStoreRecoveryStorage : IPlatformCatalogTrustStoreRecoveryStorage
{
    private readonly SqlOptions _sqlOptions;

    public SqlPlatformCatalogTrustStoreRecoveryStorage(IOptions<SqlOptions> sqlOptions)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
    }

    public string BackupBackendName => "managed-sql";
    public string BackupBackendClass => "managed_durable";
    public string CustodyBoundary => "database_managed";

    public CatalogTrustStoreRecoveryStorageWriteResult Write(string content)
    {
        using var connection = Open();
        using var command = CreateCommand(connection, "dbo.app_platform_signertrust_backup_upsert");
        command.Parameters.Add(new SqlParameter("@BackupJson", SqlDbType.NVarChar, -1) { Value = content });
        command.Parameters.Add(new SqlParameter("@BackupBackendClass", SqlDbType.NVarChar, 80) { Value = BackupBackendClass });
        command.Parameters.Add(new SqlParameter("@CustodyBoundary", SqlDbType.NVarChar, 80) { Value = CustodyBoundary });
        command.ExecuteNonQuery();
        return new CatalogTrustStoreRecoveryStorageWriteResult
        {
            BackupPath = "sql://PlatformCatalogSignerTrustBackup/current",
            BackupBackendName = BackupBackendName,
            BackupBackendClass = BackupBackendClass,
            CustodyBoundary = CustodyBoundary
        };
    }

    public CatalogTrustStoreRecoveryStorageReadResult Read()
    {
        using var connection = Open();
        using var command = CreateCommand(connection, "dbo.app_platform_signertrust_backup_get");
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return new CatalogTrustStoreRecoveryStorageReadResult
            {
                Found = false,
                BackupPath = "sql://PlatformCatalogSignerTrustBackup/current",
                BackupBackendName = BackupBackendName,
                BackupBackendClass = BackupBackendClass,
                CustodyBoundary = CustodyBoundary
            };
        }

        return new CatalogTrustStoreRecoveryStorageReadResult
        {
            Found = true,
            BackupPath = "sql://PlatformCatalogSignerTrustBackup/current",
            BackupBackendName = BackupBackendName,
            BackupBackendClass = reader["BackupBackendClass"] as string ?? BackupBackendClass,
            CustodyBoundary = reader["CustodyBoundary"] as string ?? CustodyBoundary,
            Content = reader["BackupJson"] as string ?? string.Empty
        };
    }

    private SqlConnection Open()
    {
        var connection = new SqlConnection(_sqlOptions.ConnectionString);
        connection.Open();
        return connection;
    }

    private SqlCommand CreateCommand(SqlConnection connection, string storedProcedure) => new(storedProcedure, connection)
    {
        CommandType = CommandType.StoredProcedure,
        CommandTimeout = _sqlOptions.CommandTimeoutSeconds
    };
}
