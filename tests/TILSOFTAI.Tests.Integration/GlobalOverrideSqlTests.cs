using System.Data;
using Microsoft.Data.SqlClient;
using Xunit;

namespace TILSOFTAI.Tests.Integration;

public sealed class GlobalOverrideSqlTests
{
    private readonly string? _connectionString = Environment.GetEnvironmentVariable("TILSOFTAI_TEST_CONNECTION");

    [Fact]
    public async Task MetadataDictionary_AllowsGlobalAndTenantRows()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        var tenantId = $"tenant-md-{Guid.NewGuid():N}";
        var key = $"Test.Meta.{Guid.NewGuid():N}";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            await InsertMetadataRowAsync(connection, transaction, key, null, "en", "Global Name");
            await InsertMetadataRowAsync(connection, transaction, key, tenantId, "en", "Tenant Name");

            const string countSql = """
SELECT COUNT(1)
FROM dbo.MetadataDictionary
WHERE [Key] = @Key
  AND Language = @Language
""";
            await using var command = new SqlCommand(countSql, connection, transaction)
            {
                CommandType = CommandType.Text
            };
            command.Parameters.Add(new SqlParameter("@Key", SqlDbType.NVarChar, 200) { Value = key });
            command.Parameters.Add(new SqlParameter("@Language", SqlDbType.NVarChar, 10) { Value = "en" });

            var count = Convert.ToInt32(await command.ExecuteScalarAsync());
            Assert.Equal(2, count);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    [Fact]
    public async Task MetadataDictionary_FallbackPrefersTenantThenGlobal()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        var tenantId = $"tenant-md-{Guid.NewGuid():N}";
        var key = $"Test.Meta.{Guid.NewGuid():N}";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            await InsertMetadataRowAsync(connection, transaction, key, null, "en", "Global EN");
            await InsertMetadataRowAsync(connection, transaction, key, null, "vi", "Global VI");
            await InsertMetadataRowAsync(connection, transaction, key, tenantId, "en", "Tenant EN");

            var resolved = await GetMetadataAsync(connection, transaction, tenantId, "vi", "en");
            Assert.True(resolved.TryGetValue(key, out var entry));
            Assert.Equal(tenantId, entry.TenantId);
            Assert.Equal("en", entry.Language);
            Assert.Equal("Tenant EN", entry.DisplayName);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    [Fact]
    public async Task NormalizationRule_PrefersTenantOverride()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        var tenantId = $"tenant-nr-{Guid.NewGuid():N}";
        var ruleKey = $"rule-{Guid.NewGuid():N}";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            await InsertNormalizationRuleAsync(connection, transaction, ruleKey, null, 50, "GLOBAL", "G");
            await InsertNormalizationRuleAsync(connection, transaction, ruleKey, tenantId, 1, "TENANT", "T");

            var rules = await GetNormalizationRulesAsync(connection, transaction, tenantId);
            Assert.True(rules.TryGetValue(ruleKey, out var rule));
            Assert.Equal(tenantId, rule.TenantId);
            Assert.Equal("TENANT", rule.Pattern);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    private static async Task InsertMetadataRowAsync(
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

    private static async Task<Dictionary<string, MetadataEntry>> GetMetadataAsync(
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

    private static async Task InsertNormalizationRuleAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string ruleKey,
        string? tenantId,
        int priority,
        string pattern,
        string replacement)
    {
        const string sql = """
INSERT INTO dbo.NormalizationRule
(
    RuleKey,
    TenantId,
    Priority,
    Pattern,
    Replacement,
    Description,
    IsEnabled
)
VALUES
(
    @RuleKey,
    @TenantId,
    @Priority,
    @Pattern,
    @Replacement,
    NULL,
    1
);
""";

        await using var command = new SqlCommand(sql, connection, transaction)
        {
            CommandType = CommandType.Text
        };
        command.Parameters.Add(new SqlParameter("@RuleKey", SqlDbType.NVarChar, 200) { Value = ruleKey });
        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50)
        {
            Value = tenantId is null ? DBNull.Value : tenantId
        });
        command.Parameters.Add(new SqlParameter("@Priority", SqlDbType.Int) { Value = priority });
        command.Parameters.Add(new SqlParameter("@Pattern", SqlDbType.NVarChar, 1000) { Value = pattern });
        command.Parameters.Add(new SqlParameter("@Replacement", SqlDbType.NVarChar, 1000) { Value = replacement });

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<Dictionary<string, NormalizationRuleEntry>> GetNormalizationRulesAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string tenantId)
    {
        var results = new Dictionary<string, NormalizationRuleEntry>(StringComparer.OrdinalIgnoreCase);

        await using var command = new SqlCommand("dbo.app_normalizationrule_list", connection, transaction)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });

        await using var reader = await command.ExecuteReaderAsync();
        var keyOrdinal = reader.GetOrdinal("RuleKey");
        var tenantOrdinal = reader.GetOrdinal("TenantId");
        var patternOrdinal = reader.GetOrdinal("Pattern");

        while (await reader.ReadAsync())
        {
            var key = reader.GetString(keyOrdinal);
            var tenant = reader.IsDBNull(tenantOrdinal) ? null : reader.GetString(tenantOrdinal);
            var pattern = reader.GetString(patternOrdinal);

            results[key] = new NormalizationRuleEntry(tenant, pattern);
        }

        return results;
    }

    private sealed record MetadataEntry(string? TenantId, string Language, string DisplayName);

    private sealed record NormalizationRuleEntry(string? TenantId, string Pattern);
}
