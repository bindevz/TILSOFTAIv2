using System.Data;
using Microsoft.Data.SqlClient;
using Xunit;

namespace TILSOFTAI.Tests.Contract;

public sealed class MetadataDictionarySqlTests
{
    private readonly string? _connectionString = Environment.GetEnvironmentVariable("TILSOFTAI_TEST_CONNECTION");

    [Fact]
    public async Task MetadataDictionary_ResolvesTenantAndLanguageFallback()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        var tenantId = $"tenant-md-{Guid.NewGuid():N}";
        var key1 = $"Test.Meta.{Guid.NewGuid():N}.One";
        var key2 = $"Test.Meta.{Guid.NewGuid():N}.Two";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            await InsertRowAsync(connection, transaction, key1, null, "en", "Global EN 1");
            await InsertRowAsync(connection, transaction, key1, null, "vi", "Global VI 1");
            await InsertRowAsync(connection, transaction, key1, tenantId, "en", "Tenant EN 1");

            await InsertRowAsync(connection, transaction, key2, null, "en", "Global EN 2");
            await InsertRowAsync(connection, transaction, key2, null, "vi", "Global VI 2");
            await InsertRowAsync(connection, transaction, key2, tenantId, "vi", "Tenant VI 2");

            var results = await GetDictionaryEntriesAsync(connection, transaction, tenantId, "vi", "en");

            Assert.True(results.TryGetValue(key1, out var entry1));
            Assert.Equal(tenantId, entry1.TenantId);
            Assert.Equal("en", entry1.Language);
            Assert.Equal("Tenant EN 1", entry1.DisplayName);

            Assert.True(results.TryGetValue(key2, out var entry2));
            Assert.Equal(tenantId, entry2.TenantId);
            Assert.Equal("vi", entry2.Language);
            Assert.Equal("Tenant VI 2", entry2.DisplayName);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    private static async Task InsertRowAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string key,
        string? tenantId,
        string language,
        string displayName)
    {
        const string sql = """
INSERT INTO dbo.MetadataDictionary
(
    [Key],
    TenantId,
    Language,
    DisplayName,
    Description,
    Unit,
    Examples
)
VALUES
(
    @Key,
    @TenantId,
    @Language,
    @DisplayName,
    NULL,
    NULL,
    NULL
);
""";

        await using var command = new SqlCommand(sql, connection, transaction)
        {
            CommandType = CommandType.Text
        };

        command.Parameters.Add(new SqlParameter("@Key", SqlDbType.NVarChar, 200) { Value = key });
        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50)
        {
            Value = tenantId is null ? DBNull.Value : tenantId
        });
        command.Parameters.Add(new SqlParameter("@Language", SqlDbType.NVarChar, 10) { Value = language });
        command.Parameters.Add(new SqlParameter("@DisplayName", SqlDbType.NVarChar, 200) { Value = displayName });

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<Dictionary<string, MetadataEntry>> GetDictionaryEntriesAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string tenantId,
        string language,
        string defaultLanguage)
    {
        var results = new Dictionary<string, MetadataEntry>(StringComparer.OrdinalIgnoreCase);

        await using var command = new SqlCommand("dbo.app_metadatadictionary_list", connection, transaction)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
        command.Parameters.Add(new SqlParameter("@Language", SqlDbType.NVarChar, 10) { Value = language });
        command.Parameters.Add(new SqlParameter("@DefaultLanguage", SqlDbType.NVarChar, 10) { Value = defaultLanguage });

        await using var reader = await command.ExecuteReaderAsync();
        var keyOrdinal = reader.GetOrdinal("Key");
        var tenantOrdinal = reader.GetOrdinal("TenantId");
        var languageOrdinal = reader.GetOrdinal("Language");
        var displayNameOrdinal = reader.GetOrdinal("DisplayName");

        while (await reader.ReadAsync())
        {
            var key = reader.GetString(keyOrdinal);
            var tenant = reader.IsDBNull(tenantOrdinal) ? null : reader.GetString(tenantOrdinal);
            var resolvedLanguage = reader.GetString(languageOrdinal);
            var displayName = reader.GetString(displayNameOrdinal);

            results[key] = new MetadataEntry(tenant, resolvedLanguage, displayName);
        }

        return results;
    }

    private sealed record MetadataEntry(string? TenantId, string Language, string DisplayName);
}
