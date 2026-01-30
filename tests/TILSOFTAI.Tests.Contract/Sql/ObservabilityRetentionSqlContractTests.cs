using System.Data;
using Microsoft.Data.SqlClient;
using Xunit;

namespace TILSOFTAI.Tests.Contract.Sql;

/// <summary>
/// Contract tests for observability retention and purge functionality.
/// </summary>
public sealed class ObservabilityRetentionSqlContractTests
{
    private readonly string? _connectionString = Environment.GetEnvironmentVariable("TILSOFTAI_TEST_CONNECTION");

    [Fact]
    public async Task ObservabilityPurgeProcedureExists()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        const string query = """
SELECT COUNT(1)
FROM sys.procedures
WHERE name = 'app_observability_purge'
""";

        var count = await ExecuteScalarAsync(query);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ObservabilityPurgeProcedureHasExpectedSignature()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        // Verify the procedure has the expected parameters
        const string query = """
SELECT 
    p.name AS ProcedureName,
    COUNT(CASE WHEN pm.name = 'RetentionDays' THEN 1 END) AS HasRetentionDays,
    COUNT(CASE WHEN pm.name = 'BatchSize' THEN 1 END) AS HasBatchSize,
    COUNT(CASE WHEN pm.name = 'TenantId' THEN 1 END) AS HasTenantId
FROM sys.procedures p
LEFT JOIN sys.parameters pm ON p.object_id = pm.object_id
WHERE p.name = 'app_observability_purge'
GROUP BY p.name
""";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(query, connection) { CommandType = CommandType.Text };
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync(), "Procedure app_observability_purge not found");
        
        var hasRetentionDays = reader.GetInt32(reader.GetOrdinal("HasRetentionDays"));
        var hasBatchSize = reader.GetInt32(reader.GetOrdinal("HasBatchSize"));
        var hasTenantId = reader.GetInt32(reader.GetOrdinal("HasTenantId"));

        Assert.Equal(1, hasRetentionDays);
        Assert.Equal(1, hasBatchSize);
        Assert.Equal(1, hasTenantId);
    }

    [Fact]
    public void SqlAgentJobScriptFileExists()
    {
        // Verify the SQL Agent job script file exists
        var projectRoot = FindProjectRoot();
        var jobScriptPath = Path.Combine(projectRoot, "sql", "01_core", "007_jobs_observability_purge.sql");
        
        Assert.True(File.Exists(jobScriptPath), $"SQL Agent job script not found at: {jobScriptPath}");
    }

    private async Task<int> ExecuteScalarAsync(string query)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(query, connection) { CommandType = CommandType.Text };
        var result = await command.ExecuteScalarAsync();
        return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
    }

    private static string FindProjectRoot()
    {
        var directory = Directory.GetCurrentDirectory();
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory, "sql")))
            {
                return directory;
            }
            directory = Directory.GetParent(directory)?.FullName;
        }
        throw new InvalidOperationException("Could not find project root (sql directory)");
    }
}
