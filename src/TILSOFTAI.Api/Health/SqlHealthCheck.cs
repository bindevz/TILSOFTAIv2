using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Api.Health;

/// <summary>
/// Health check for SQL Server connectivity.
/// Used for readiness probes to ensure database is accessible.
/// </summary>
public sealed class SqlHealthCheck : IHealthCheck
{
    private readonly SqlOptions _sqlOptions;

    public SqlHealthCheck(IOptions<SqlOptions> sqlOptions)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = 5; // 5 second timeout for health checks
            
            await command.ExecuteScalarAsync(cancellationToken);
            
            return HealthCheckResult.Healthy("SQL Server is accessible");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server is not accessible", ex);
        }
    }
}
