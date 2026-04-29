using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Actions;

namespace TILSOFTAI.Infrastructure.Actions;

public sealed class SqlActionRequestStore : IActionRequestStore
{
    private readonly SqlOptions _sqlOptions;

    public SqlActionRequestStore(IOptions<SqlOptions> sqlOptions)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
    }

    public async Task<ActionRequestRecord> CreateAsync(ActionRequestCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var record = ActionRequestRecord.FromCreateRequest(request, DateTime.UtcNow);
        return await CreateAsync(record, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ActionRequestRecord> CreateAsync(ActionRequestRecord request, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand("dbo.app_actionrequest_create", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _sqlOptions.CommandTimeoutSeconds
        };

        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = request.TenantId });
        command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.NVarChar, 50) { Value = DbValue(request.UserId) });
        command.Parameters.Add(new SqlParameter("@ConversationId", SqlDbType.NVarChar, 64) { Value = request.ConversationId });
        command.Parameters.Add(new SqlParameter("@CapabilityKey", SqlDbType.NVarChar, 200) { Value = DbValue(request.CapabilityKey) });
        command.Parameters.Add(new SqlParameter("@FunctionName", SqlDbType.NVarChar, 200) { Value = DbValue(request.FunctionName) });
        command.Parameters.Add(new SqlParameter("@ProposedToolName", SqlDbType.NVarChar, 200) { Value = request.ProposedToolName });
        command.Parameters.Add(new SqlParameter("@ProposedSpName", SqlDbType.NVarChar, 200) { Value = request.ProposedSpName });
        command.Parameters.Add(new SqlParameter("@ArgsJson", SqlDbType.NVarChar, -1) { Value = request.ArgsJson });
        command.Parameters.Add(new SqlParameter("@PreviewResultJson", SqlDbType.NVarChar, -1) { Value = DbValue(request.PreviewResultJson) });
        command.Parameters.Add(new SqlParameter("@RequestedByUserId", SqlDbType.NVarChar, 50) { Value = request.RequestedByUserId });
        command.Parameters.Add(new SqlParameter("@ExpiresAtUtc", SqlDbType.DateTime2) { Value = request.ExpiresAtUtc == default ? DateTime.UtcNow.AddHours(24) : request.ExpiresAtUtc });
        command.Parameters.Add(new SqlParameter("@CorrelationId", SqlDbType.NVarChar, 100) { Value = DbValue(request.CorrelationId) });
        command.Parameters.Add(new SqlParameter("@MetadataJson", SqlDbType.NVarChar, -1) { Value = DbValue(request.MetadataJson) });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadSingleAsync(reader, cancellationToken)
            ?? throw new InvalidOperationException("Failed to create action request.");
    }

    public async Task<ActionRequestRecord?> GetAsync(string tenantId, string actionId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand("dbo.app_actionrequest_get", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _sqlOptions.CommandTimeoutSeconds
        };

        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
        command.Parameters.Add(new SqlParameter("@ActionId", SqlDbType.NVarChar, 64) { Value = actionId });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadSingleAsync(reader, cancellationToken);
    }

    public async Task<ActionRequestRecord?> GetActiveForConversationAsync(
        string tenantId,
        string userId,
        string conversationId,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand("dbo.app_actionrequest_get_active_for_conversation", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _sqlOptions.CommandTimeoutSeconds
        };

        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
        command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.NVarChar, 50) { Value = userId });
        command.Parameters.Add(new SqlParameter("@ConversationId", SqlDbType.NVarChar, 64) { Value = conversationId });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadSingleAsync(reader, cancellationToken);
    }

    public async Task<ActionRequestRecord> ConfirmAsync(
        string tenantId,
        string userId,
        string actionId,
        CancellationToken cancellationToken) =>
        await ExecuteUserStatusChangeAsync(
            "dbo.app_actionrequest_confirm",
            tenantId,
            userId,
            actionId,
            reason: null,
            cancellationToken).ConfigureAwait(false);

    public async Task<ActionRequestRecord> ApproveAsync(string tenantId, string actionId, string approvedByUserId, CancellationToken cancellationToken)
    {
        return await ExecuteStatusChangeAsync("dbo.app_actionrequest_approve", tenantId, actionId, approvedByUserId, cancellationToken);
    }

    public async Task<ActionRequestRecord> RejectAsync(
        string tenantId,
        string userId,
        string actionId,
        string? reason,
        CancellationToken cancellationToken) =>
        await ExecuteUserStatusChangeAsync(
            "dbo.app_actionrequest_reject",
            tenantId,
            userId,
            actionId,
            reason,
            cancellationToken).ConfigureAwait(false);

    public async Task<ActionRequestRecord> MarkExecutedAsync(string tenantId, string actionId, string resultCompactJson, bool success, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand("dbo.app_actionrequest_mark_executed", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _sqlOptions.CommandTimeoutSeconds
        };

        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
        command.Parameters.Add(new SqlParameter("@ActionId", SqlDbType.NVarChar, 64) { Value = actionId });
        command.Parameters.Add(new SqlParameter("@ResultCompactJson", SqlDbType.NVarChar, -1) { Value = resultCompactJson });
        command.Parameters.Add(new SqlParameter("@Success", SqlDbType.Bit) { Value = success });
        command.Parameters.Add(new SqlParameter("@ExecutedByUserId", SqlDbType.NVarChar, 50) { Value = DBNull.Value });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadSingleAsync(reader, cancellationToken)
            ?? throw new InvalidOperationException("Failed to mark action as executed.");
    }

    public async Task<ActionRequestRecord> MarkExecutedAsync(
        string tenantId,
        string actionId,
        string executedByUserId,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand("dbo.app_actionrequest_mark_executed", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _sqlOptions.CommandTimeoutSeconds
        };

        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
        command.Parameters.Add(new SqlParameter("@ActionId", SqlDbType.NVarChar, 64) { Value = actionId });
        command.Parameters.Add(new SqlParameter("@ResultCompactJson", SqlDbType.NVarChar, -1) { Value = DBNull.Value });
        command.Parameters.Add(new SqlParameter("@Success", SqlDbType.Bit) { Value = true });
        command.Parameters.Add(new SqlParameter("@ExecutedByUserId", SqlDbType.NVarChar, 50) { Value = executedByUserId });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadSingleAsync(reader, cancellationToken)
            ?? throw new InvalidOperationException("Failed to mark action as executed.");
    }

    public async Task<int> ExpireOldAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand("dbo.app_actionrequest_expire_old", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _sqlOptions.CommandTimeoutSeconds
        };

        command.Parameters.Add(new SqlParameter("@NowUtc", SqlDbType.DateTime2) { Value = nowUtc.UtcDateTime });
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is int count ? count : Convert.ToInt32(result ?? 0);
    }

    private async Task<ActionRequestRecord> ExecuteStatusChangeAsync(
        string storedProcedure,
        string tenantId,
        string actionId,
        string approvedByUserId,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(storedProcedure, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _sqlOptions.CommandTimeoutSeconds
        };

        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
        command.Parameters.Add(new SqlParameter("@ActionId", SqlDbType.NVarChar, 64) { Value = actionId });
        command.Parameters.Add(new SqlParameter("@ApprovedByUserId", SqlDbType.NVarChar, 50) { Value = approvedByUserId });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadSingleAsync(reader, cancellationToken)
            ?? throw new InvalidOperationException("Action request was not updated.");
    }

    private async Task<ActionRequestRecord> ExecuteUserStatusChangeAsync(
        string storedProcedure,
        string tenantId,
        string userId,
        string actionId,
        string? reason,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(storedProcedure, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _sqlOptions.CommandTimeoutSeconds
        };

        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
        command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.NVarChar, 50) { Value = userId });
        command.Parameters.Add(new SqlParameter("@ActionId", SqlDbType.NVarChar, 64) { Value = actionId });
        command.Parameters.Add(new SqlParameter("@Reason", SqlDbType.NVarChar, 500) { Value = DbValue(reason) });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadSingleAsync(reader, cancellationToken)
            ?? throw new InvalidOperationException("Action request was not updated.");
    }

    private static async Task<ActionRequestRecord?> ReadSingleAsync(SqlDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var columns = GetColumnNames(reader);
        var record = new ActionRequestRecord
        {
            ActionId = reader["ActionId"] as string ?? string.Empty,
            TenantId = reader["TenantId"] as string ?? string.Empty,
            ConversationId = reader["ConversationId"] as string ?? string.Empty,
            RequestedAtUtc = reader["RequestedAtUtc"] != DBNull.Value
                ? (DateTime)reader["RequestedAtUtc"]
                : DateTime.MinValue,
            Status = reader["Status"] as string ?? string.Empty,
            ProposedToolName = reader["ProposedToolName"] as string ?? string.Empty,
            ProposedSpName = reader["ProposedSpName"] as string ?? string.Empty,
            ArgsJson = reader["ArgsJson"] as string ?? string.Empty,
            RequestedByUserId = reader["RequestedByUserId"] as string ?? string.Empty,
            ApprovedByUserId = reader["ApprovedByUserId"] as string,
            ApprovedAtUtc = reader["ApprovedAtUtc"] != DBNull.Value ? (DateTime?)reader["ApprovedAtUtc"] : null,
            ExecutedAtUtc = reader["ExecutedAtUtc"] != DBNull.Value ? (DateTime?)reader["ExecutedAtUtc"] : null,
            ExecutionResultCompactJson = reader["ExecutionResultCompactJson"] as string
        };

        record.UserId = ReadString(reader, columns, "UserId") ?? record.RequestedByUserId;
        record.CapabilityKey = ReadString(reader, columns, "CapabilityKey") ?? record.ProposedToolName;
        record.FunctionName = ReadString(reader, columns, "FunctionName") ?? record.ProposedToolName;
        record.PreviewResultJson = ReadString(reader, columns, "PreviewResultJson");
        record.ExpiresAtUtc = ReadDateTime(reader, columns, "ExpiresAtUtc") ?? record.RequestedAtUtc.AddHours(24);
        record.ConfirmedAtUtc = ReadDateTime(reader, columns, "ConfirmedAtUtc");
        record.CancelledAtUtc = ReadDateTime(reader, columns, "CancelledAtUtc");
        record.ExecutedByUserId = ReadString(reader, columns, "ExecutedByUserId");
        record.CorrelationId = ReadString(reader, columns, "CorrelationId");
        record.MetadataJson = ReadString(reader, columns, "MetadataJson");
        return record;
    }

    private static object DbValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

    private static HashSet<string> GetColumnNames(SqlDataReader reader)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < reader.FieldCount; index++)
        {
            columns.Add(reader.GetName(index));
        }

        return columns;
    }

    private static string? ReadString(SqlDataReader reader, HashSet<string> columns, string name) =>
        columns.Contains(name) && reader[name] != DBNull.Value ? reader[name] as string : null;

    private static DateTime? ReadDateTime(SqlDataReader reader, HashSet<string> columns, string name) =>
        columns.Contains(name) && reader[name] != DBNull.Value ? (DateTime?)reader[name] : null;
}
