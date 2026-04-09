using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Properties;
using TILSOFTAI.Orchestration.Actions;
using TILSOFTAI.Orchestration.Compaction;
using TILSOFTAI.Orchestration.Conversations;
using TILSOFTAI.Orchestration.Tools;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Approvals;

public sealed class ApprovalEngine : IApprovalEngine
{
    private const string SqlAdapterType = "sql";
    private const string WriteActionCatalogStoredProcedure = "dbo.app_writeactioncatalog_get";

    private readonly IActionRequestStore _requestStore;
    private readonly IToolAdapterRegistry _toolAdapterRegistry;
    private readonly ToolResultCompactor _toolResultCompactor;
    private readonly IConversationStore _conversationStore;
    private readonly ChatOptions _chatOptions;
    private readonly IJsonSchemaValidator _schemaValidator;
    private readonly ILogger<ApprovalEngine> _logger;

    public ApprovalEngine(
        IActionRequestStore requestStore,
        IToolAdapterRegistry toolAdapterRegistry,
        ToolResultCompactor toolResultCompactor,
        IConversationStore conversationStore,
        IOptions<ChatOptions> chatOptions,
        IJsonSchemaValidator schemaValidator,
        ILogger<ApprovalEngine> logger)
    {
        _requestStore = requestStore ?? throw new ArgumentNullException(nameof(requestStore));
        _toolAdapterRegistry = toolAdapterRegistry ?? throw new ArgumentNullException(nameof(toolAdapterRegistry));
        _toolResultCompactor = toolResultCompactor ?? throw new ArgumentNullException(nameof(toolResultCompactor));
        _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
        _chatOptions = chatOptions?.Value ?? throw new ArgumentNullException(nameof(chatOptions));
        _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProposedActionRecord> CreateAsync(ProposedAction action, ApprovalContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);

        var proposedSpName = action.StoredProcedure ?? string.Empty;
        if (string.IsNullOrWhiteSpace(proposedSpName))
        {
            throw new ArgumentException(Resources.Val_ProposedSpNameRequired, nameof(action));
        }

        if (proposedSpName.StartsWith("ai_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(Resources.Ex_WriteActionsMustNotExecuteAiStoredProcedures);
        }

        var catalogEntry = await GetCatalogEntryAsync(context, proposedSpName, ct);
        if (catalogEntry == null)
        {
            throw new InvalidOperationException(string.Format(Resources.Ex_WriteActionNotAllowedOrNotFound, proposedSpName));
        }

        if (!catalogEntry.IsEnabled)
        {
            throw new InvalidOperationException(string.Format(Resources.Ex_WriteActionCurrentlyDisabled, proposedSpName));
        }

        ValidateRoles(catalogEntry.RequiredRoles, context.Roles, executionPhase: false);
        ValidatePayloadSchema(catalogEntry.JsonSchema, action.PayloadJson, executionPhase: false);

        var requestRecord = new ActionRequestRecord
        {
            TenantId = context.TenantId,
            ConversationId = context.ConversationId,
            Status = "Pending",
            ProposedToolName = action.ToolName ?? action.CapabilityKey,
            ProposedSpName = proposedSpName,
            ArgsJson = action.PayloadJson,
            RequestedByUserId = context.UserId
        };

        var created = await _requestStore.CreateAsync(requestRecord, ct);

        _logger.LogInformation(
            "ApprovalCreate | ActionId: {ActionId} | Tenant: {TenantId} | Agent: {AgentId} | SP: {StoredProcedure} | RequestedBy: {UserId}",
            created.ActionId, context.TenantId, action.AgentId, proposedSpName, context.UserId);

        return MapRecord(created, action, context.AgentId);
    }

    public async Task<ProposedActionRecord> ApproveAsync(string actionId, ApprovalContext context, CancellationToken ct)
    {
        var record = await _requestStore.ApproveAsync(context.TenantId, actionId, context.UserId, ct);

        _logger.LogInformation(
            "ApprovalApprove | ActionId: {ActionId} | Tenant: {TenantId} | ApprovedBy: {UserId}",
            actionId, context.TenantId, context.UserId);

        return MapRecord(record, actionType: "write", agentId: context.AgentId, targetSystem: SqlAdapterType);
    }

    public async Task<ProposedActionRecord> RejectAsync(string actionId, ApprovalContext context, CancellationToken ct)
    {
        var record = await _requestStore.RejectAsync(context.TenantId, actionId, context.UserId, ct);

        _logger.LogInformation(
            "ApprovalReject | ActionId: {ActionId} | Tenant: {TenantId} | RejectedBy: {UserId}",
            actionId, context.TenantId, context.UserId);

        return MapRecord(record, actionType: "write", agentId: context.AgentId, targetSystem: SqlAdapterType);
    }

    public async Task<ActionExecutionResult> ExecuteAsync(string actionId, ApprovalContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = await _requestStore.GetAsync(context.TenantId, actionId, ct);
        if (request is null)
        {
            throw new InvalidOperationException(Resources.Ex_ActionRequestNotFound);
        }

        if (!string.Equals(request.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(Resources.Ex_ActionRequestMustBeApproved);
        }

        var catalogEntry = await GetCatalogEntryAsync(context, request.ProposedSpName, ct);
        if (catalogEntry == null)
        {
            throw new InvalidOperationException(string.Format(Resources.Ex_WriteActionNoLongerAllowed, request.ProposedSpName));
        }

        if (!catalogEntry.IsEnabled)
        {
            throw new InvalidOperationException(string.Format(Resources.Ex_WriteActionHasBeenDisabled, request.ProposedSpName));
        }

        ValidateRoles(catalogEntry.RequiredRoles, context.Roles, executionPhase: true);
        ValidatePayloadSchema(catalogEntry.JsonSchema, request.ArgsJson, executionPhase: true);

        var sw = Stopwatch.StartNew();
        var adapter = _toolAdapterRegistry.Resolve(SqlAdapterType);
        var execution = await adapter.ExecuteAsync(
            new ToolExecutionRequest
            {
                TenantId = context.TenantId,
                AgentId = context.AgentId ?? "approval-engine",
                SystemId = SqlAdapterType,
                CapabilityKey = request.ProposedToolName,
                Operation = ToolAdapterOperationNames.ExecuteWriteAction,
                ArgumentsJson = request.ArgsJson,
                CorrelationId = context.CorrelationId,
                Metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["storedProcedure"] = request.ProposedSpName,
                    ["approvedActionId"] = actionId
                }
            },
            ct);

        if (!execution.Success)
        {
            throw new InvalidOperationException(
                $"Write action execution failed: {execution.ErrorCode ?? "UNKNOWN_ERROR"}");
        }

        var rawResult = execution.PayloadJson ?? string.Empty;
        var maxBytes = _chatOptions.CompactionLimits.TryGetValue("ToolResultMaxBytes", out var limit) && limit > 0
            ? limit
            : 16000;
        var compacted = _toolResultCompactor.CompactJson(rawResult, maxBytes, _chatOptions.CompactionRules);

        sw.Stop();
        var updated = await _requestStore.MarkExecutedAsync(context.TenantId, actionId, compacted, success: true, ct);

        _logger.LogInformation(
            "ApprovalExecute | ActionId: {ActionId} | Tenant: {TenantId} | SP: {StoredProcedure} | DurationMs: {DurationMs} | Success: true",
            actionId, context.TenantId, request.ProposedSpName, sw.ElapsedMilliseconds);

        await _conversationStore.SaveToolExecutionAsync(
            new Domain.ExecutionContext.TilsoftExecutionContext
            {
                TenantId = context.TenantId,
                UserId = context.UserId,
                Roles = context.Roles.ToArray(),
                ConversationId = context.ConversationId,
                CorrelationId = context.CorrelationId
            },
            new ToolExecutionRecord
            {
                ToolName = request.ProposedToolName,
                SpName = request.ProposedSpName,
                ArgumentsJson = request.ArgsJson,
                Result = rawResult,
                CompactedResult = compacted,
                Success = true,
                DurationMs = 0
            },
            RequestPolicy.Default,
            ct);

        return new ActionExecutionResult
        {
            Action = MapRecord(updated, actionType: "write", agentId: context.AgentId, targetSystem: SqlAdapterType),
            RawResult = rawResult,
            CompactedResult = compacted
        };
    }

    private async Task<CatalogEntry?> GetCatalogEntryAsync(ApprovalContext context, string spName, CancellationToken ct)
    {
        var adapter = _toolAdapterRegistry.Resolve(SqlAdapterType);
        var parametersJson = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["@TenantId"] = context.TenantId,
            ["@SpName"] = spName
        });

        var result = await adapter.ExecuteAsync(
            new ToolExecutionRequest
            {
                TenantId = context.TenantId,
                AgentId = context.AgentId ?? "approval-engine",
                SystemId = SqlAdapterType,
                CapabilityKey = "writeaction.catalog.get",
                Operation = ToolAdapterOperationNames.ExecuteQuery,
                ArgumentsJson = parametersJson,
                CorrelationId = context.CorrelationId,
                Metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["storedProcedure"] = WriteActionCatalogStoredProcedure
                }
            },
            ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Catalog lookup failed: {result.ErrorCode ?? "UNKNOWN_ERROR"}");
        }

        if (result.Payload is IReadOnlyList<IReadOnlyDictionary<string, object?>> rows && rows.Count > 0)
        {
            return MapCatalogEntry(rows[0]);
        }

        if (string.IsNullOrWhiteSpace(result.PayloadJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(result.PayloadJson);
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        var firstRow = document.RootElement[0];
        return new CatalogEntry
        {
            ActionName = firstRow.TryGetProperty("ActionName", out var actionName) ? actionName.GetString() : null,
            RequiredRoles = firstRow.TryGetProperty("RequiredRoles", out var requiredRoles) ? requiredRoles.GetString() : null,
            JsonSchema = firstRow.TryGetProperty("JsonSchema", out var jsonSchema) ? jsonSchema.GetString() : null,
            IsEnabled = true
        };
    }

    private static CatalogEntry MapCatalogEntry(IReadOnlyDictionary<string, object?> row) => new()
    {
        ActionName = row.TryGetValue("ActionName", out var actionName) ? actionName?.ToString() : null,
        RequiredRoles = row.TryGetValue("RequiredRoles", out var requiredRoles) ? requiredRoles?.ToString() : null,
        JsonSchema = row.TryGetValue("JsonSchema", out var jsonSchema) ? jsonSchema?.ToString() : null,
        IsEnabled = true
    };

    private static void ValidateRoles(string? requiredRoles, IReadOnlyList<string> actualRoles, bool executionPhase)
    {
        if (string.IsNullOrWhiteSpace(requiredRoles))
        {
            return;
        }

        var required = requiredRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (required.Any(role => actualRoles.Contains(role, StringComparer.OrdinalIgnoreCase)))
        {
            return;
        }

        var suffix = executionPhase ? " for execution." : " for this action.";
        throw new UnauthorizedAccessException(
            $"User does not have required roles ({requiredRoles}){suffix}");
    }

    private void ValidatePayloadSchema(string? jsonSchema, string payloadJson, bool executionPhase)
    {
        if (!string.IsNullOrWhiteSpace(jsonSchema))
        {
            var validation = _schemaValidator.Validate(jsonSchema, payloadJson);
            if (!validation.IsValid)
            {
                var errorDetail = validation.Errors.Count > 0
                    ? string.Join("; ", validation.Errors)
                    : string.IsNullOrWhiteSpace(validation.Summary)
                        ? executionPhase
                            ? "Arguments no longer match the current schema."
                            : "Arguments do not match the required schema."
                        : validation.Summary;

                throw new ArgumentException(
                    string.Format(
                        executionPhase
                            ? Resources.Val_SchemaValidationFailedAtExecutionTime
                            : Resources.Val_SchemaValidationFailed,
                        errorDetail));
            }

            return;
        }

        try
        {
            using var _ = JsonDocument.Parse(payloadJson);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(string.Format(Resources.Val_ArgumentsMustBeValidJson, ex.Message));
        }
    }

    private static ProposedActionRecord MapRecord(
        ActionRequestRecord record,
        ProposedAction action,
        string? agentId) => new()
    {
        ActionId = record.ActionId,
        TenantId = record.TenantId,
        ConversationId = record.ConversationId,
        RequestedAtUtc = record.RequestedAtUtc,
        Status = record.Status,
        ActionType = action.ActionType,
        AgentId = string.IsNullOrWhiteSpace(action.AgentId) ? agentId ?? string.Empty : action.AgentId,
        TargetSystem = action.TargetSystem,
        CapabilityKey = action.CapabilityKey,
        ToolName = record.ProposedToolName,
        StoredProcedure = record.ProposedSpName,
        PayloadJson = record.ArgsJson,
        DiffPreviewJson = action.DiffPreviewJson,
        RiskLevel = action.RiskLevel,
        ApprovalRequirement = action.ApprovalRequirement,
        RequestedByUserId = record.RequestedByUserId,
        ApprovedByUserId = record.ApprovedByUserId,
        ApprovedAtUtc = record.ApprovedAtUtc,
        ExecutedAtUtc = record.ExecutedAtUtc,
        ExecutionResultCompactJson = record.ExecutionResultCompactJson
    };

    private static ProposedActionRecord MapRecord(
        ActionRequestRecord record,
        string actionType,
        string? agentId,
        string targetSystem) => new()
    {
        ActionId = record.ActionId,
        TenantId = record.TenantId,
        ConversationId = record.ConversationId,
        RequestedAtUtc = record.RequestedAtUtc,
        Status = record.Status,
        ActionType = actionType,
        AgentId = agentId ?? string.Empty,
        TargetSystem = targetSystem,
        CapabilityKey = record.ProposedToolName,
        ToolName = record.ProposedToolName,
        StoredProcedure = record.ProposedSpName,
        PayloadJson = record.ArgsJson,
        RequestedByUserId = record.RequestedByUserId,
        ApprovedByUserId = record.ApprovedByUserId,
        ApprovedAtUtc = record.ApprovedAtUtc,
        ExecutedAtUtc = record.ExecutedAtUtc,
        ExecutionResultCompactJson = record.ExecutionResultCompactJson
    };

    private sealed class CatalogEntry
    {
        public string? ActionName { get; init; }
        public string? RequiredRoles { get; init; }
        public string? JsonSchema { get; init; }
        public bool IsEnabled { get; init; }
    }
}
