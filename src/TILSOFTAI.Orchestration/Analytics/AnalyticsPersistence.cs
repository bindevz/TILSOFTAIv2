using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Orchestration.Analytics;

/// <summary>
/// PATCH 29.06: Persists analytics task frames and validation errors for audit trail.
/// </summary>
public sealed class AnalyticsPersistence
{
    private readonly SqlOptions _sqlOptions;
    private readonly AnalyticsOptions _analyticsOptions;
    private readonly ILogger<AnalyticsPersistence> _logger;

    public AnalyticsPersistence(
        IOptions<SqlOptions> sqlOptions,
        IOptions<AnalyticsOptions> analyticsOptions,
        ILogger<AnalyticsPersistence> logger)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
        _analyticsOptions = analyticsOptions?.Value ?? throw new ArgumentNullException(nameof(analyticsOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Saves a task frame for audit/debugging.
    /// </summary>
    public async Task SaveTaskFrameAsync(
        string tenantId,
        string conversationId,
        string requestId,
        string taskType,
        string? entity,
        object? metrics,
        object? filters,
        object? breakdowns,
        string? timeRangeHint,
        bool needsVisualization,
        decimal? confidence,
        CancellationToken ct)
    {
        if (!_analyticsOptions.EnableTaskFramePersistence)
        {
            return;
        }

        try
        {
            await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.app_analytics_taskframe_save";
            command.CommandType = System.Data.CommandType.StoredProcedure;

            command.Parameters.AddWithValue("@TenantId", tenantId);
            command.Parameters.AddWithValue("@ConversationId", conversationId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@RequestId", requestId);
            command.Parameters.AddWithValue("@TaskType", taskType);
            command.Parameters.AddWithValue("@Entity", entity ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@MetricsJson", metrics != null ? JsonSerializer.Serialize(metrics) : DBNull.Value);
            command.Parameters.AddWithValue("@FiltersJson", filters != null ? JsonSerializer.Serialize(filters) : DBNull.Value);
            command.Parameters.AddWithValue("@BreakdownsJson", breakdowns != null ? JsonSerializer.Serialize(breakdowns) : DBNull.Value);
            command.Parameters.AddWithValue("@TimeRangeHint", timeRangeHint ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@NeedsVisualization", needsVisualization);
            command.Parameters.AddWithValue("@Confidence", confidence ?? (object)DBNull.Value);

            await command.ExecuteNonQueryAsync(ct);
            _logger.LogDebug("TaskFrame saved | RequestId: {RequestId}", requestId);
        }
        catch (SqlException ex)
        {
            // Log but don't fail the main flow for persistence errors
            _logger.LogWarning(ex, "Failed to save TaskFrame | RequestId: {RequestId}", requestId);
        }
    }

    /// <summary>
    /// Saves a plan validation error for debugging.
    /// </summary>
    public async Task SaveValidationErrorAsync(
        string tenantId,
        string requestId,
        string errorCode,
        string? errorMessage,
        IEnumerable<string>? suggestions,
        string? planJson,
        bool retryable,
        int retryCount,
        CancellationToken ct)
    {
        if (!_analyticsOptions.EnableTaskFramePersistence)
        {
            return;
        }

        try
        {
            await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.app_analytics_planvalidationerror_save";
            command.CommandType = System.Data.CommandType.StoredProcedure;

            command.Parameters.AddWithValue("@TenantId", tenantId);
            command.Parameters.AddWithValue("@RequestId", requestId);
            command.Parameters.AddWithValue("@ErrorCode", errorCode);
            command.Parameters.AddWithValue("@ErrorMessage", errorMessage ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@SuggestionsJson", suggestions != null ? JsonSerializer.Serialize(suggestions) : DBNull.Value);
            command.Parameters.AddWithValue("@PlanJson", planJson ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Retryable", retryable);
            command.Parameters.AddWithValue("@RetryCount", retryCount);

            await command.ExecuteNonQueryAsync(ct);
            _logger.LogDebug("ValidationError saved | RequestId: {RequestId} | Code: {ErrorCode}", requestId, errorCode);
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Failed to save ValidationError | RequestId: {RequestId}", requestId);
        }
    }
}
