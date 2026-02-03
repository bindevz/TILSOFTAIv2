using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Audit;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Audit;

/// <summary>
/// Writes audit events to SQL Server.
/// </summary>
public sealed class SqlAuditSink : IAuditSink
{
    private readonly SqlOptions _sqlOptions;
    private readonly AuditOptions _auditOptions;
    private readonly ILogger<SqlAuditSink> _logger;

    public string Name => "SQL";
    public bool IsEnabled => _auditOptions.SqlEnabled;

    public SqlAuditSink(
        IOptions<SqlOptions> sqlOptions,
        IOptions<AuditOptions> auditOptions,
        ILogger<SqlAuditSink> logger)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
        _auditOptions = auditOptions?.Value ?? throw new ArgumentNullException(nameof(auditOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task WriteBatchAsync(IReadOnlyList<AuditEvent> events, CancellationToken ct)
    {
        if (events.Count == 0) return;

        try
        {
            await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
            await connection.OpenAsync(ct);

            // Use TVP for efficient batch insert
            var dataTable = CreateDataTable(events);

            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.app_auditlog_insert";
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = _sqlOptions.CommandTimeoutSeconds;

            var param = command.Parameters.AddWithValue("@Events", dataTable);
            param.SqlDbType = SqlDbType.Structured;
            param.TypeName = "dbo.AuditEventTableType";

            await command.ExecuteNonQueryAsync(ct);

            _logger.LogDebug("Wrote {Count} audit events to SQL", events.Count);
        }
        catch (SqlException ex) when (ex.Number == 208) // Invalid object name - table doesn't exist
        {
            _logger.LogWarning("Audit table not found. Falling back to individual inserts. Error: {Message}", ex.Message);
            await WriteBatchFallbackAsync(events, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write {Count} audit events to SQL", events.Count);
            throw;
        }
    }

    private async Task WriteBatchFallbackAsync(IReadOnlyList<AuditEvent> events, CancellationToken ct)
    {
        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(ct);

        foreach (var evt in events)
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO dbo.AuditLog (
                        EventId, EventType, Timestamp, TenantId, UserId, CorrelationId,
                        IpAddress, UserAgent, Outcome, Details, Checksum, CreatedAtUtc
                    ) VALUES (
                        @EventId, @EventType, @Timestamp, @TenantId, @UserId, @CorrelationId,
                        @IpAddress, @UserAgent, @Outcome, @Details, @Checksum, @CreatedAtUtc
                    )";
                command.CommandTimeout = _sqlOptions.CommandTimeoutSeconds;

                command.Parameters.AddWithValue("@EventId", evt.EventId);
                command.Parameters.AddWithValue("@EventType", (int)evt.EventType);
                command.Parameters.AddWithValue("@Timestamp", evt.Timestamp);
                command.Parameters.AddWithValue("@TenantId", evt.TenantId);
                command.Parameters.AddWithValue("@UserId", evt.UserId);
                command.Parameters.AddWithValue("@CorrelationId", evt.CorrelationId);
                command.Parameters.AddWithValue("@IpAddress", evt.IpAddress);
                command.Parameters.AddWithValue("@UserAgent", TruncateUserAgent(evt.UserAgent));
                command.Parameters.AddWithValue("@Outcome", (int)evt.Outcome);
                command.Parameters.AddWithValue("@Details", evt.Details?.RootElement.GetRawText() ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Checksum", evt.Checksum);
                command.Parameters.AddWithValue("@CreatedAtUtc", DateTimeOffset.UtcNow);

                await command.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to insert individual audit event {EventId}", evt.EventId);
            }
        }
    }

    private DataTable CreateDataTable(IReadOnlyList<AuditEvent> events)
    {
        var table = new DataTable();
        table.Columns.Add("EventId", typeof(Guid));
        table.Columns.Add("EventType", typeof(int));
        table.Columns.Add("Timestamp", typeof(DateTimeOffset));
        table.Columns.Add("TenantId", typeof(string));
        table.Columns.Add("UserId", typeof(string));
        table.Columns.Add("CorrelationId", typeof(string));
        table.Columns.Add("IpAddress", typeof(string));
        table.Columns.Add("UserAgent", typeof(string));
        table.Columns.Add("Outcome", typeof(int));
        table.Columns.Add("Details", typeof(string));
        table.Columns.Add("Checksum", typeof(string));

        foreach (var evt in events)
        {
            table.Rows.Add(
                evt.EventId,
                (int)evt.EventType,
                evt.Timestamp,
                evt.TenantId,
                evt.UserId,
                evt.CorrelationId,
                evt.IpAddress,
                TruncateUserAgent(evt.UserAgent),
                (int)evt.Outcome,
                evt.Details?.RootElement.GetRawText(),
                evt.Checksum
            );
        }

        return table;
    }

    private static string TruncateUserAgent(string userAgent)
    {
        const int maxLength = 500;
        return userAgent.Length > maxLength ? userAgent[..maxLength] : userAgent;
    }
}
