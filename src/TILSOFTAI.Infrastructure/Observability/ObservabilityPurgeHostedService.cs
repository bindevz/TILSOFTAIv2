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
/// Only runs if ObservabilityOptions.PurgeEnabled is true.
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
        var purgeEnabled = _observabilityOptions.Value.PurgeEnabled;
        
        if (!purgeEnabled)
        {
            _logger.LogInformation("Observability purge service disabled. Set Observability:PurgeEnabled=true to enable.");
            return Task.CompletedTask;
        }

        var purgeHour = _observabilityOptions.Value.PurgeRunHourUtc;
        _logger.LogInformation(
            "Observability purge service starting. Purge will run daily at {PurgeHour}:00 UTC.",
            purgeHour);
        
        // Calculate time until next purge
        var now = DateTime.UtcNow;
        var nextRun = new DateTime(now.Year, now.Month, now.Day, purgeHour, 0, 0, DateTimeKind.Utc);
        
        if (nextRun <= now)
        {
            // If the time has already passed today, schedule for tomorrow
            nextRun = nextRun.AddDays(1);
        }
        
        var initialDelay = nextRun - now;
        var period = TimeSpan.FromHours(24);
        
        _logger.LogInformation(
            "First purge scheduled for {NextRun:yyyy-MM-dd HH:mm:ss} UTC (in {Hours}h {Minutes}m)",
            nextRun, (int)initialDelay.TotalHours, initialDelay.Minutes);
        
        _timer = new Timer(ExecutePurge, null, initialDelay, period);
        
        return Task.CompletedTask;
    }

    private async void ExecutePurge(object? state)
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
            await connection.OpenAsync();
            
            await using var command = new SqlCommand("dbo.app_observability_purge", connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 300 // 5 minutes for large purges
            };
            
            command.Parameters.AddWithValue("@RetentionDays", retentionDays);
            command.Parameters.AddWithValue("@BatchSize", batchSize);
            command.Parameters.AddWithValue("@TenantId", DBNull.Value); // Purge all tenants
            
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
