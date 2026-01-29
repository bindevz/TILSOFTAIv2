using System.Data;
using Microsoft.Data.SqlClient;
using Xunit;

namespace TILSOFTAI.Tests.Contract;

public sealed class SqlSchemaLintTests
{
    private readonly string? _connectionString = Environment.GetEnvironmentVariable("TILSOFTAI_TEST_CONNECTION");

    [Fact]
    public async Task PrimaryKeysMustNotIncludeNullableColumns()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        const string query = """
SELECT
    t.name AS TableName,
    c.name AS ColumnName
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
JOIN sys.tables t ON t.object_id = i.object_id
WHERE i.is_primary_key = 1
  AND c.is_nullable = 1;
""";

        var rows = new List<(string Table, string Column)>();
        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            await using var command = new SqlCommand(query, connection) { CommandType = CommandType.Text };
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        if (rows.Count > 0)
        {
            var details = string.Join(", ", rows.Select(row => $"{row.Table}.{row.Column}"));
            Assert.Fail($"Primary keys include nullable columns: {details}");
        }
    }

    [Theory]
    [InlineData("UX_MetadataDictionary_Global_Key_Lang", "TenantId IS NULL")]
    [InlineData("UX_MetadataDictionary_Tenant_Key_Tenant_Lang", "TenantId IS NOT NULL")]
    [InlineData("UX_NormalizationRule_Global_RuleKey", "TenantId IS NULL")]
    [InlineData("UX_NormalizationRule_Tenant_RuleKey_Tenant", "TenantId IS NOT NULL")]
    [InlineData("UX_DatasetCatalog_Global_DatasetKey", "TenantId IS NULL")]
    [InlineData("UX_DatasetCatalog_Tenant_DatasetKey_Tenant", "TenantId IS NOT NULL")]
    [InlineData("UX_DiagnosticsRule_Global_Module_RuleKey", "TenantId IS NULL")]
    [InlineData("UX_DiagnosticsRule_Tenant_Module_RuleKey_Tenant", "TenantId IS NOT NULL")]
    public async Task RequiredFilteredUniqueIndexesMustExist(string indexName, string expectedFilter)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        const string query = """
SELECT
    i.name,
    i.has_filter,
    i.filter_definition
FROM sys.indexes i
WHERE i.name = @IndexName;
""";

        string? name = null;
        int hasFilter = 0;
        string? filterDefinition = null;

        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            await using var command = new SqlCommand(query, connection) { CommandType = CommandType.Text };
            command.Parameters.Add(new SqlParameter("@IndexName", SqlDbType.NVarChar, 200) { Value = indexName });
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                name = reader.IsDBNull(0) ? null : reader.GetString(0);
                hasFilter = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                filterDefinition = reader.IsDBNull(2) ? null : reader.GetString(2);
            }
        }

        Assert.False(string.IsNullOrWhiteSpace(name), $"Missing filtered unique index: {indexName}");
        Assert.Equal(1, hasFilter);
        Assert.True(
            !string.IsNullOrWhiteSpace(filterDefinition) && filterDefinition.Contains(expectedFilter, StringComparison.OrdinalIgnoreCase),
            $"Index {indexName} has_filter={hasFilter}, filter_definition='{filterDefinition}'");
    }
}
