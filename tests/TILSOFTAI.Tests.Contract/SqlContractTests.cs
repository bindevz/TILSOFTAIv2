using System.Data;
using Microsoft.Data.SqlClient;
using Xunit;

namespace TILSOFTAI.Tests.Contract;

public sealed class SqlContractTests
{
    private readonly string? _connectionString = Environment.GetEnvironmentVariable("TILSOFTAI_TEST_CONNECTION");

    [Fact]
    public async Task ToolCatalogSpPrefixEnforced()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        const string query = """
SELECT COUNT(1)
FROM dbo.ToolCatalog
WHERE IsEnabled = 1
  AND (SpName IS NULL OR SpName NOT LIKE 'ai[_]%')
""";

        var count = await ExecuteScalarAsync(query);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task EnabledToolsHaveExistingAiSp()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        const string query = """
SELECT COUNT(1)
FROM dbo.ToolCatalog tc
WHERE tc.IsEnabled = 1
  AND tc.SpName IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM sys.procedures p
      WHERE p.name = tc.SpName OR CONCAT(SCHEMA_NAME(p.schema_id), '.', p.name) = tc.SpName
  )
""";

        var count = await ExecuteScalarAsync(query);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task RequiredAppSpsExist()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        var required = new[]
        {
            "app_errorlog_insert",
            "app_conversation_upsert",
            "app_conversationmessage_insert",
            "app_toolexecution_insert",
            "app_toolcatalog_list",
            "app_metadatadictionary_list"
        };

        const string query = """
SELECT p.name
FROM sys.procedures p
""";

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            await using var command = new SqlCommand(query, connection) { CommandType = CommandType.Text };
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                existing.Add(reader.GetString(0));
            }
        }

        foreach (var name in required)
        {
            Assert.Contains(name, existing);
        }
    }

    [Fact]
    public async Task ToolCatalogAlignmentHasInstructionAndSchema()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        const string query = """
SELECT COUNT(1)
FROM dbo.ToolCatalog
WHERE IsEnabled = 1
  AND (JsonSchema IS NULL OR Instruction IS NULL OR LTRIM(RTRIM(JsonSchema)) = '' OR LTRIM(RTRIM(Instruction)) = '')
""";

        var count = await ExecuteScalarAsync(query);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ModelAiProceduresExistOnce()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        var required = new[]
        {
            "ai_model_get_overview",
            "ai_model_get_pieces",
            "ai_model_get_materials",
            "ai_model_compare_models"
        };

        const string query = """
SELECT name, COUNT(1) AS Cnt
FROM sys.procedures
WHERE name IN ('ai_model_get_overview', 'ai_model_get_pieces', 'ai_model_get_materials', 'ai_model_compare_models')
GROUP BY name
""";

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            await using var command = new SqlCommand(query, connection) { CommandType = CommandType.Text };
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                counts[reader.GetString(0)] = reader.GetInt32(1);
            }
        }

        foreach (var name in required)
        {
            Assert.True(counts.TryGetValue(name, out var count), $"Missing stored procedure: {name}");
            Assert.Equal(1, count);
        }
    }

    private async Task<int> ExecuteScalarAsync(string query)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(query, connection) { CommandType = CommandType.Text };
        var result = await command.ExecuteScalarAsync();
        return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
    }
}
