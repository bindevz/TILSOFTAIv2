using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Observability;

namespace TILSOFTAI.Infrastructure.Errors;

public sealed class SqlErrorLogWriter : ISqlErrorLogWriter
{
    private readonly SqlOptions _sqlOptions;
    private readonly ObservabilityOptions _observabilityOptions;
    private readonly ILogRedactor _logRedactor;

    public SqlErrorLogWriter(
        IOptions<SqlOptions> sqlOptions,
        IOptions<ObservabilityOptions> observabilityOptions,
        ILogRedactor logRedactor)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
        _observabilityOptions = observabilityOptions?.Value ?? throw new ArgumentNullException(nameof(observabilityOptions));
        _logRedactor = logRedactor ?? throw new ArgumentNullException(nameof(logRedactor));
    }

    public async Task WriteAsync(TilsoftExecutionContext context, string code, string message, object? detail, CancellationToken ct)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var tenantId = string.IsNullOrWhiteSpace(context.TenantId) ? "unknown" : context.TenantId;
        var errorId = Guid.NewGuid().ToString("N");
        var safeMessage = message ?? string.Empty;
        
        // Redact if enabled
        if (_observabilityOptions.RedactLogs && !string.IsNullOrWhiteSpace(safeMessage))
        {
            safeMessage = _logRedactor.RedactText(safeMessage).redacted;
        }
        
        if (safeMessage.Length > 2000)
        {
            safeMessage = safeMessage[..2000];
        }

        var detailJson = SerializeDetail(detail);
        
        // Redact detail JSON if enabled
        if (_observabilityOptions.RedactLogs && !string.IsNullOrWhiteSpace(detailJson))
        {
            detailJson = _logRedactor.RedactJson(detailJson).redacted;
        }

        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand("dbo.app_errorlog_insert", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _sqlOptions.CommandTimeoutSeconds
        };

        command.Parameters.Add(new SqlParameter("@TenantId", tenantId));
        command.Parameters.Add(new SqlParameter("@ErrorId", errorId));
        command.Parameters.Add(new SqlParameter("@CorrelationId", DbNullIfEmpty(context.CorrelationId)));
        command.Parameters.Add(new SqlParameter("@ConversationId", DbNullIfEmpty(context.ConversationId)));
        command.Parameters.Add(new SqlParameter("@TraceId", DbNullIfEmpty(context.TraceId)));
        command.Parameters.Add(new SqlParameter("@RequestId", DbNullIfEmpty(context.RequestId)));
        command.Parameters.Add(new SqlParameter("@UserId", DbNullIfEmpty(context.UserId)));
        command.Parameters.Add(new SqlParameter("@ErrorCode", code ?? string.Empty));
        command.Parameters.Add(new SqlParameter("@Message", safeMessage));
        command.Parameters.Add(new SqlParameter("@DetailJson", DbNullIfEmpty(detailJson)));

        await command.ExecuteNonQueryAsync(ct);
    }

    private static object DbNullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

    private static string? SerializeDetail(object? detail)
    {
        if (detail is null)
        {
            return null;
        }

        if (detail is string text)
        {
            return text;
        }

        return JsonSerializer.Serialize(detail);
    }
}
