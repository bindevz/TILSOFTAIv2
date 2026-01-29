using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Compaction;
using TILSOFTAI.Orchestration.Conversations;
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

    public ActionApprovalService(
        IActionRequestStore requestStore,
        IToolHandler toolHandler,
        ISqlExecutor sqlExecutor,
        ToolResultCompactor toolResultCompactor,
        IConversationStore conversationStore,
        IOptions<ChatOptions> chatOptions)
    {
        _requestStore = requestStore ?? throw new ArgumentNullException(nameof(requestStore));
        _toolHandler = toolHandler ?? throw new ArgumentNullException(nameof(toolHandler));
        _sqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));
        _toolResultCompactor = toolResultCompactor ?? throw new ArgumentNullException(nameof(toolResultCompactor));
        _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
        _chatOptions = chatOptions?.Value ?? throw new ArgumentNullException(nameof(chatOptions));
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
            throw new ArgumentException("Proposed SP name is required.", nameof(proposedSpName));
        }

        if (proposedSpName.StartsWith("ai_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Write actions must not execute ai_ stored procedures directly.");
        }

        // Validate against Catalog
        var catalogEntry = await GetCatalogEntryAsync(context.TenantId, proposedSpName, ct);
        if (catalogEntry == null)
        {
            throw new InvalidOperationException($"Write action '{proposedSpName}' is not allowed or not found in the WriteActionCatalog.");
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

        // Basic JSON Validation
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(argsJson);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new ArgumentException("Arguments must be valid JSON.");
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
            throw new InvalidOperationException("Action request not found.");
        }

        if (!string.Equals(request.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Action request must be approved before execution.");
        }

        // Re-validate Catalog and Roles at execution time
        var catalogEntry = await GetCatalogEntryAsync(context.TenantId, request.ProposedSpName, ct);
        if (catalogEntry == null)
        {
             throw new InvalidOperationException($"Write action '{request.ProposedSpName}' is no longer allowed.");
        }

        if (!string.IsNullOrWhiteSpace(catalogEntry.RequiredRoles))
        {
            var required = catalogEntry.RequiredRoles.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (!required.Any(r => context.Roles.Contains(r.Trim(), StringComparer.OrdinalIgnoreCase)))
            {
                throw new UnauthorizedAccessException($"User does not have required roles ({catalogEntry.RequiredRoles}) for execution.");
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
        }, ct);

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
            RequiredRoles = row.ContainsKey("RequiredRoles") ? row["RequiredRoles"]?.ToString() : null
        };
    }

    private class CatalogEntry
    {
        public string? RequiredRoles { get; set; }
    }
}
