using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Properties;
using TILSOFTAI.Orchestration.Compaction;
using TILSOFTAI.Orchestration.Conversations;
using TILSOFTAI.Orchestration.Tools;
using TILSOFTAI.Orchestration.Sql;

namespace TILSOFTAI.Orchestration.Actions;

public sealed class ActionApprovalService
{
    private readonly IActionRequestStore _requestStore;
    private readonly IToolHandler _toolHandler;
    private readonly ISqlExecutor _sqlExecutor;
    private readonly ToolResultCompactor _toolResultCompactor;
    private readonly IConversationStore _conversationStore;
    private readonly ChatOptions _chatOptions;
    private readonly IJsonSchemaValidator _schemaValidator;

    public ActionApprovalService(
        IActionRequestStore requestStore,
        IToolHandler toolHandler,
        ISqlExecutor sqlExecutor,
        ToolResultCompactor toolResultCompactor,
        IConversationStore conversationStore,
        IOptions<ChatOptions> chatOptions,
        IJsonSchemaValidator schemaValidator)
    {
        _requestStore = requestStore ?? throw new ArgumentNullException(nameof(requestStore));
        _toolHandler = toolHandler ?? throw new ArgumentNullException(nameof(toolHandler));
        _sqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));
        _toolResultCompactor = toolResultCompactor ?? throw new ArgumentNullException(nameof(toolResultCompactor));
        _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
        _chatOptions = chatOptions?.Value ?? throw new ArgumentNullException(nameof(chatOptions));
        _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
    }

    public async Task<ActionRequestRecord> CreateAsync(
        TilsoftExecutionContext context,
        string toolName,
        string proposedSpName,
        string argsJson,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(proposedSpName))
        {
            throw new ArgumentException(Resources.Val_ProposedSpNameRequired, nameof(proposedSpName));
        }

        if (proposedSpName.StartsWith("ai_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(Resources.Ex_WriteActionsMustNotExecuteAiStoredProcedures);
        }

        // Validate against Catalog
        var catalogEntry = await GetCatalogEntryAsync(context.TenantId, proposedSpName, ct);
        if (catalogEntry == null)
        {
            throw new InvalidOperationException(string.Format(Resources.Ex_WriteActionNotAllowedOrNotFound, proposedSpName));
        }

        // Check IsEnabled flag
        if (!catalogEntry.IsEnabled)
        {
            throw new InvalidOperationException(string.Format(Resources.Ex_WriteActionCurrentlyDisabled, proposedSpName));
        }

        // Validate Roles
        if (!string.IsNullOrWhiteSpace(catalogEntry.RequiredRoles))
        {
            var required = catalogEntry.RequiredRoles.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (!required.Any(r => context.Roles.Contains(r.Trim(), StringComparer.OrdinalIgnoreCase)))
            {
                throw new UnauthorizedAccessException($"User does not have required roles ({catalogEntry.RequiredRoles}) for this action.");
            }
        }

        // Validate JSON Schema
        if (!string.IsNullOrWhiteSpace(catalogEntry.JsonSchema))
        {
            var validation = _schemaValidator.Validate(catalogEntry.JsonSchema, argsJson);
            if (!validation.IsValid)
            {
                var errorDetail = validation.Errors.Count > 0
                    ? string.Join("; ", validation.Errors)
                    : string.IsNullOrWhiteSpace(validation.Summary)
                        ? "Arguments do not match the required schema."
                        : validation.Summary;
                throw new ArgumentException(string.Format(Resources.Val_SchemaValidationFailed, errorDetail));
            }
        }
        else
        {
            // Fallback: Basic JSON parse validation if no schema defined
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(argsJson);
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new ArgumentException(string.Format(Resources.Val_ArgumentsMustBeValidJson, ex.Message));
            }
        }

        var request = new ActionRequestRecord
        {
            TenantId = context.TenantId,
            ConversationId = context.ConversationId,
            Status = "Pending",
            ProposedToolName = toolName,
            ProposedSpName = proposedSpName,
            ArgsJson = argsJson,
            RequestedByUserId = context.UserId
        };

        return await _requestStore.CreateAsync(request, ct);
    }

    public Task<ActionRequestRecord> ApproveAsync(TilsoftExecutionContext context, string actionId, CancellationToken ct)
    {
        return _requestStore.ApproveAsync(context.TenantId, actionId, context.UserId, ct);
    }

    public Task<ActionRequestRecord> RejectAsync(TilsoftExecutionContext context, string actionId, CancellationToken ct)
    {
        return _requestStore.RejectAsync(context.TenantId, actionId, context.UserId, ct);
    }

    public async Task<ActionExecutionResult> ExecuteAsync(TilsoftExecutionContext context, string actionId, CancellationToken ct)
    {
        var request = await _requestStore.GetAsync(context.TenantId, actionId, ct);
        if (request is null)
        {
            throw new InvalidOperationException(Resources.Ex_ActionRequestNotFound);
        }

        if (!string.Equals(request.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(Resources.Ex_ActionRequestMustBeApproved);
        }

        // Re-validate Catalog and Roles at execution time (defense-in-depth)
        var catalogEntry = await GetCatalogEntryAsync(context.TenantId, request.ProposedSpName, ct);
        if (catalogEntry == null)
        {
             throw new InvalidOperationException(string.Format(Resources.Ex_WriteActionNoLongerAllowed, request.ProposedSpName));
        }

        // Re-check IsEnabled flag
        if (!catalogEntry.IsEnabled)
        {
            throw new InvalidOperationException(string.Format(Resources.Ex_WriteActionHasBeenDisabled, request.ProposedSpName));
        }

        // Re-validate Roles
        if (!string.IsNullOrWhiteSpace(catalogEntry.RequiredRoles))
        {
            var required = catalogEntry.RequiredRoles.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (!required.Any(r => context.Roles.Contains(r.Trim(), StringComparer.OrdinalIgnoreCase)))
            {
                throw new UnauthorizedAccessException($"User does not have required roles ({catalogEntry.RequiredRoles}) for execution.");
            }
        }

        // Re-validate JSON Schema (catalog could have changed)
        if (!string.IsNullOrWhiteSpace(catalogEntry.JsonSchema))
        {
            var validation = _schemaValidator.Validate(catalogEntry.JsonSchema, request.ArgsJson);
            if (!validation.IsValid)
            {
                var errorDetail = validation.Errors.Count > 0
                    ? string.Join("; ", validation.Errors)
                    : string.IsNullOrWhiteSpace(validation.Summary)
                        ? "Arguments no longer match the current schema."
                        : validation.Summary;
                throw new ArgumentException(string.Format(Resources.Val_SchemaValidationFailedAtExecutionTime, errorDetail));
            }
        }

        // Execute via ExecuteWriteActionAsync (bypassing prefix check, enforcing catalog)
        var rawResult = await _sqlExecutor.ExecuteWriteActionAsync(request.ProposedSpName, context.TenantId, request.ArgsJson, ct);
        
        var maxBytes = _chatOptions.CompactionLimits.TryGetValue("ToolResultMaxBytes", out var limit) && limit > 0
            ? limit
            : 16000;
        var compacted = _toolResultCompactor.CompactJson(rawResult, maxBytes, _chatOptions.CompactionRules);

        var updated = await _requestStore.MarkExecutedAsync(context.TenantId, actionId, compacted, success: true, ct);

        await _conversationStore.SaveToolExecutionAsync(context, new ToolExecutionRecord
        {
            ToolName = request.ProposedToolName,
            SpName = request.ProposedSpName,
            ArgumentsJson = request.ArgsJson,
            Result = rawResult,
            CompactedResult = compacted,
            Success = true,
            DurationMs = 0
        }, RequestPolicy.Default, ct);

        return new ActionExecutionResult
        {
            ActionRequest = updated,
            RawResult = rawResult,
            CompactedResult = compacted
        };
    }

    private async Task<CatalogEntry?> GetCatalogEntryAsync(string tenantId, string spName, CancellationToken ct)
    {
        var results = await _sqlExecutor.ExecuteQueryAsync(
            "dbo.app_writeactioncatalog_get",
            new Dictionary<string, object?>
            {
                ["@TenantId"] = tenantId,
                ["@SpName"] = spName
            },
            ct);

        if (results.Count == 0) return null;
        var row = results[0];
        
        return new CatalogEntry
        {
            ActionName = row.ContainsKey("ActionName") ? row["ActionName"]?.ToString() : null,
            RequiredRoles = row.ContainsKey("RequiredRoles") ? row["RequiredRoles"]?.ToString() : null,
            JsonSchema = row.ContainsKey("JsonSchema") ? row["JsonSchema"]?.ToString() : null,
            IsEnabled = true // SQL SP already filters by IsEnabled=1
        };
    }

    private class CatalogEntry
    {
        public string? ActionName { get; set; }
        public string? RequiredRoles { get; set; }
        public string? JsonSchema { get; set; }
        public bool IsEnabled { get; set; }
    }
}
