using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Conversations;
using TILSOFTAI.Orchestration.Observability;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Infrastructure.Conversations;

public sealed class SqlConversationStore : IConversationStore
{
    private readonly SqlOptions _sqlOptions;
    private readonly ObservabilityOptions _observabilityOptions;
    private readonly ILogRedactor _logRedactor;

    public SqlConversationStore(
        IOptions<SqlOptions> sqlOptions, 
        IOptions<ObservabilityOptions> observabilityOptions,
        ILogRedactor logRedactor)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
        _observabilityOptions = observabilityOptions?.Value ?? throw new ArgumentNullException(nameof(observabilityOptions));
        _logRedactor = logRedactor ?? throw new ArgumentNullException(nameof(logRedactor));
    }

    public async Task SaveUserMessageAsync(TilsoftExecutionContext context, ChatMessage message, CancellationToken cancellationToken = default)
    {
        await SaveMessageAsync(context, message, cancellationToken);
    }

    public async Task SaveAssistantMessageAsync(TilsoftExecutionContext context, ChatMessage message, CancellationToken cancellationToken = default)
    {
        await SaveMessageAsync(context, message, cancellationToken);
    }

    public async Task SaveToolExecutionAsync(TilsoftExecutionContext context, ToolExecutionRecord execution, CancellationToken cancellationToken = default)
    {
        if (!ShouldPersistConversation() || !_observabilityOptions.EnableSqlToolLog)
        {
            return;
        }

        ValidateContext(context);

        await UpsertConversationAsync(context, cancellationToken);

        var executionId = Guid.NewGuid().ToString("N");

        // Redact tool execution data if enabled
        var argsJson = execution.ArgumentsJson;
        var resultJson = execution.Result;
        var compactedJson = execution.CompactedResult;

        if (_observabilityOptions.RedactLogs)
        {
            argsJson = _logRedactor.RedactJson(argsJson ?? string.Empty).redacted;
            if (resultJson != null)
            {
                resultJson = _logRedactor.RedactJson(resultJson).redacted;
            }
            if (compactedJson != null)
            {
                compactedJson = _logRedactor.RedactJson(compactedJson).redacted;
            }
        }

        await ExecuteAsync(
            "dbo.app_toolexecution_insert",
            new Dictionary<string, object?>
            {
                ["@TenantId"] = context.TenantId,
                ["@ConversationId"] = context.ConversationId,
                ["@ExecutionId"] = executionId,
                ["@ToolName"] = execution.ToolName,
                ["@SpName"] = execution.SpName ?? string.Empty,
                ["@ArgumentsJson"] = argsJson,
                ["@ResultJson"] = resultJson,
                ["@CompactedResultJson"] = compactedJson,
                ["@Success"] = execution.Success,
                ["@DurationMs"] = execution.DurationMs,
                ["@CorrelationId"] = context.CorrelationId,
                ["@TraceId"] = context.TraceId,
                ["@RequestId"] = context.RequestId,
                ["@UserId"] = context.UserId
            },
            cancellationToken);
    }

    private async Task SaveMessageAsync(TilsoftExecutionContext context, ChatMessage message, CancellationToken cancellationToken)
    {
        if (!ShouldPersistConversation())
        {
            return;
        }

        ValidateContext(context);

        await UpsertConversationAsync(context, cancellationToken);

        var messageId = Guid.NewGuid().ToString("N");

        // Redact message content if enabled
        var content = message.Content;
        var isRedacted = false;

        if (_observabilityOptions.RedactLogs && !string.IsNullOrWhiteSpace(content))
        {
            var (redacted, changed) = _logRedactor.RedactText(content);
            content = redacted;
            isRedacted = changed;
        }

        await ExecuteAsync(
            "dbo.app_conversationmessage_insert",
            new Dictionary<string, object?>
            {
                ["@TenantId"] = context.TenantId,
                ["@ConversationId"] = context.ConversationId,
                ["@MessageId"] = messageId,
                ["@Role"] = message.Role,
                ["@Content"] = content,
                ["@ToolName"] = string.IsNullOrWhiteSpace(message.Name) ? DBNull.Value : message.Name,
                ["@CorrelationId"] = context.CorrelationId,
                ["@TraceId"] = context.TraceId,
                ["@RequestId"] = context.RequestId,
                ["@UserId"] = context.UserId,
                ["@Language"] = context.Language,
                ["@IsRedacted"] = isRedacted
            },
            cancellationToken);
    }

    private bool ShouldPersistConversation() => _observabilityOptions.EnableConversationPersistence;

    private async Task UpsertConversationAsync(TilsoftExecutionContext context, CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            "dbo.app_conversation_upsert",
            new Dictionary<string, object?>
            {
                ["@TenantId"] = context.TenantId,
                ["@ConversationId"] = context.ConversationId,
                ["@UserId"] = context.UserId,
                ["@Language"] = context.Language,
                ["@CorrelationId"] = context.CorrelationId,
                ["@TraceId"] = context.TraceId
            },
            cancellationToken);
    }

    private async Task ExecuteAsync(string storedProcedure, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(storedProcedure, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _sqlOptions.CommandTimeoutSeconds
        };

        foreach (var (name, value) in parameters)
        {
            command.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ValidateContext(TilsoftExecutionContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (string.IsNullOrWhiteSpace(context.TenantId))
        {
            throw new InvalidOperationException("Execution context TenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(context.ConversationId))
        {
            throw new InvalidOperationException("Execution context ConversationId is required.");
        }

        if (string.IsNullOrWhiteSpace(context.UserId))
        {
            throw new InvalidOperationException("Execution context UserId is required.");
        }
    }
}
