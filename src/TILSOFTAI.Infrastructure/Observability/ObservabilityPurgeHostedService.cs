using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Observability;

/// <summary>
/// Background hosted service that purges old observability data daily.
/// Calls dbo.app_observability_purge based on ObservabilityOptions.RetentionDays.
/// </summary>
public sealed class ObservabilityPurgeHostedService : IHostedService, IDisposable
{
    private readonly IOptions<ObservabilityOptions> _observabilityOptions;
    private readonly IOptions<SqlOptions> _sqlOptions;
    private readonly ILogger<ObservabilityPurgeHostedService> _logger;
    private Timer? _timer;

    public ObservabilityPurgeHostedService(
        IOptions<ObservabilityOptions> observabilityOptions,
        IOptions<SqlOptions> sqlOptions,
        ILogger<ObservabilityPurgeHostedService> logger)
    {
        _observabilityOptions = observabilityOptions ?? throw new ArgumentNullException(nameof(observabilityOptions));
        _sqlOptions = sqlOptions ?? throw new ArgumentNullException(nameof(sqlOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Observability purge service starting. Purge will run daily at startup+1min, then every 24 hours.");
        
        // Run once on startup after 1 minute, then every 24 hours
        _timer = new Timer(ExecutePurge, null, TimeSpan.FromMinutes(1), TimeSpan.FromHours(24));
        
        return Task.CompletedTask;
    }

    private async void ExecutePurge(object? state)
    {
        var retentionDays = _observabilityOptions.Value.RetentionDays;
        
        _logger.LogInformation("Starting observability purge. Retention: {RetentionDays} days", retentionDays);
        
        try
        {
            await using var connection = new SqlConnection(_sqlOptions.Value.ConnectionString);
            await connection.OpenAsync();
            
            await using var command = new SqlCommand("dbo.app_observability_purge", connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 300 // 5 minutes for large purges
            };
            
            command.Parameters.AddWithValue("@TenantId", DBNull.Value); // Purge all tenants
            command.Parameters.AddWithValue("@OlderThanDays", retentionDays);
            
            await using var reader = await command.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                var cutoffDate = reader.GetDateTime(reader.GetOrdinal("CutoffDate"));
                var deletedMessages = reader.GetInt32(reader.GetOrdinal("DeletedMessages"));
                var deletedTools = reader.GetInt32(reader.GetOrdinal("DeletedToolExecutions"));
                var deletedConversations = reader.GetInt32(reader.GetOrdinal("DeletedConversations"));
                var deletedErrors = reader.GetInt32(reader.GetOrdinal("DeletedErrors"));
                var totalDeleted = reader.GetInt32(reader.GetOrdinal("TotalDeleted"));
                
                _logger.LogInformation(
                    "Purge completed. Cutoff: {CutoffDate:yyyy-MM-dd HH:mm:ss} UTC. Deleted: {TotalDeleted} records " +
                    "(Messages: {Messages}, Tools: {Tools}, Conversations: {Conversations}, Errors: {Errors})",
                    cutoffDate, totalDeleted, deletedMessages, deletedTools, deletedConversations, deletedErrors);
            }
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL error executing observability purge. Procedure may not exist or database unavailable.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing observability purge");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Observability purge service stopping.");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
