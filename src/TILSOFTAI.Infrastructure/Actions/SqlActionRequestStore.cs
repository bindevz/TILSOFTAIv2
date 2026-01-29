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
        command.Parameters.Add(new SqlParameter("@ConversationId", SqlDbType.NVarChar, 64) { Value = request.ConversationId });
        command.Parameters.Add(new SqlParameter("@ProposedToolName", SqlDbType.NVarChar, 200) { Value = request.ProposedToolName });
        command.Parameters.Add(new SqlParameter("@ProposedSpName", SqlDbType.NVarChar, 200) { Value = request.ProposedSpName });
        command.Parameters.Add(new SqlParameter("@ArgsJson", SqlDbType.NVarChar, -1) { Value = request.ArgsJson });
        command.Parameters.Add(new SqlParameter("@RequestedByUserId", SqlDbType.NVarChar, 50) { Value = request.RequestedByUserId });

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

    public async Task<ActionRequestRecord> ApproveAsync(string tenantId, string actionId, string approvedByUserId, CancellationToken cancellationToken)
    {
        return await ExecuteStatusChangeAsync("dbo.app_actionrequest_approve", tenantId, actionId, approvedByUserId, cancellationToken);
    }

    public async Task<ActionRequestRecord> RejectAsync(string tenantId, string actionId, string approvedByUserId, CancellationToken cancellationToken)
    {
        return await ExecuteStatusChangeAsync("dbo.app_actionrequest_reject", tenantId, actionId, approvedByUserId, cancellationToken);
    }

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

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadSingleAsync(reader, cancellationToken)
            ?? throw new InvalidOperationException("Failed to mark action as executed.");
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

    private static async Task<ActionRequestRecord?> ReadSingleAsync(SqlDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ActionRequestRecord
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
    }
}
