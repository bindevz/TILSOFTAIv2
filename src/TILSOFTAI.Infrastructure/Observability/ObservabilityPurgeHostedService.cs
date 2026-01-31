using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Observability;

/// <summary>
/// Background hosted service that purges old observability data periodically.
/// Calls dbo.app_observability_purge based on ObservabilityOptions.RetentionDays.
/// Only runs if ObservabilityOptions.PurgeEnabled is true.
/// Uses PeriodicTimer for clean cancellation and configurable intervals.
/// </summary>
public sealed class ObservabilityPurgeHostedService : BackgroundService
{
    private readonly IOptions<ObservabilityOptions> _observabilityOptions;
    private readonly IOptions<SqlOptions> _sqlOptions;
    private readonly ILogger<ObservabilityPurgeHostedService> _logger;

    public ObservabilityPurgeHostedService(
        IOptions<ObservabilityOptions> observabilityOptions,
        IOptions<SqlOptions> sqlOptions,
        ILogger<ObservabilityPurgeHostedService> logger)
    {
        _observabilityOptions = observabilityOptions ?? throw new ArgumentNullException(nameof(observabilityOptions));
        _sqlOptions = sqlOptions ?? throw new ArgumentNullException(nameof(sqlOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_observabilityOptions.Value.PurgeEnabled)
        {
            _logger.LogInformation("Observability purge service disabled. Set Observability:PurgeEnabled=true to enable.");
            return;
        }

        var intervalMinutes = _observabilityOptions.Value.PurgeIntervalMinutes;
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        _logger.LogInformation(
            "Observability purge service starting. Interval: {IntervalMinutes} minutes",
            intervalMinutes);

        using var timer = new PeriodicTimer(interval);

        try
        {
            // Execute purge immediately on startup, then at intervals
            while (!stoppingToken.IsCancellationRequested)
            {
                await ExecutePurgeAsync(stoppingToken);
                
                // Wait for next tick or cancellation
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Observability purge service cancelled.");
        }
    }

    private async Task ExecutePurgeAsync(CancellationToken cancellationToken)
    {
        if (!_observabilityOptions.Value.PurgeEnabled)
        {
            _logger.LogWarning("Purge triggered but PurgeEnabled is false. Skipping.");
            return;
        }

        var retentionDays = _observabilityOptions.Value.RetentionDays;
        var batchSize = _observabilityOptions.Value.PurgeBatchSize;
        
        _logger.LogInformation(
            "Starting observability purge. Retention: {RetentionDays} days, BatchSize: {BatchSize}",
            retentionDays, batchSize);
        
        try
        {
            await using var connection = new SqlConnection(_sqlOptions.Value.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            
            await using var command = new SqlCommand("dbo.app_observability_purge", connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 300 // 5 minutes for large purges
            };
            
            command.Parameters.AddWithValue("@RetentionDays", retentionDays);
            command.Parameters.AddWithValue("@BatchSize", batchSize);
            command.Parameters.AddWithValue("@TenantId", DBNull.Value); // Purge all tenants
            
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
            if (await reader.ReadAsync(cancellationToken))
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Purge operation cancelled.");
            throw; // Re-throw to stop the service properly
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing observability purge");
        }
    }
}
