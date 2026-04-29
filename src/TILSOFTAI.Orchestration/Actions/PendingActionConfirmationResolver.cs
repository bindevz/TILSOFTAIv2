using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.Observability;

namespace TILSOFTAI.Orchestration.Actions;

public sealed class PendingActionConfirmationResolver : IPendingActionConfirmationResolver
{
    private static readonly HashSet<string> ConfirmationPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "confirm",
        "yes",
        "y",
        "ok",
        "okay",
        "xac nhan",
        "xác nhận",
        "dong y",
        "đồng ý",
        "đong y"
    };

    private readonly IActionRequestStore _store;
    private readonly ILogger<PendingActionConfirmationResolver>? _logger;

    public PendingActionConfirmationResolver(
        IActionRequestStore store,
        ILogger<PendingActionConfirmationResolver>? logger = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger;
    }

    public async Task<PendingActionConfirmationResult?> TryResolveAsync(
        string input,
        TilsoftExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (!IsConfirmationIntent(input))
        {
            return null;
        }

        var expiredCount = await _store.ExpireOldAsync(DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        if (expiredCount > 0)
        {
            _logger?.LogInformation(
                "{EventName} | correlationId: {CorrelationId} | tenantId: {TenantId} | userId: {UserId} | conversationId: {ConversationId} | expiredCount: {ExpiredCount}",
                Sprint35TraceEvents.PendingActionExpired,
                context.CorrelationId,
                context.TenantId,
                context.UserId,
                context.ConversationId,
                expiredCount);
        }

        var pending = await _store.GetActiveForConversationAsync(
                context.TenantId,
                context.UserId,
                context.ConversationId,
                cancellationToken)
            .ConfigureAwait(false);

        if (pending is null)
        {
            return new PendingActionConfirmationResult(
                true,
                CreateNoPendingAnswer(context));
        }

        if (pending.IsExpired(DateTime.UtcNow))
        {
            return new PendingActionConfirmationResult(
                true,
                CreateNoPendingAnswer(context));
        }

        var confirmed = await _store.ConfirmAsync(
                context.TenantId,
                context.UserId,
                pending.ActionId,
                cancellationToken)
            .ConfigureAwait(false);
        _logger?.LogInformation(
            "{EventName} | correlationId: {CorrelationId} | tenantId: {TenantId} | userId: {UserId} | conversationId: {ConversationId} | actionId: {ActionId} | capabilityKey: {CapabilityKey}",
            Sprint35TraceEvents.PendingActionConfirmed,
            context.CorrelationId,
            context.TenantId,
            context.UserId,
            context.ConversationId,
            confirmed.ActionId,
            confirmed.CapabilityKey);

        return new PendingActionConfirmationResult(
            true,
            CreateConfirmedAnswer(context, confirmed));
    }

    private static bool IsConfirmationIntent(string? input)
    {
        var normalized = Normalize(input);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return ConfirmationPhrases.Contains(normalized);
    }

    private static AssistantAnswer CreateConfirmedAnswer(TilsoftExecutionContext context, ActionRequestRecord action)
    {
        var text = $"Confirmed pending action {action.ActionId}. It is ready for approval review; no write has been executed.";
        var draftAction = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["actionId"] = action.ActionId,
            ["tenantId"] = action.TenantId,
            ["conversationId"] = action.ConversationId,
            ["status"] = action.Status,
            ["capabilityKey"] = action.CapabilityKey,
            ["functionName"] = action.FunctionName,
            ["proposedToolName"] = action.ProposedToolName,
            ["proposedProcedureName"] = action.ProposedProcedureName,
            ["expiresAtUtc"] = action.ExpiresAtUtc
        };

        return new AssistantAnswer
        {
            AnswerType = "confirmation_ready",
            Text = text,
            Blocks = new AnswerBlock[]
            {
                new SummaryBlock(text),
                new ConfirmationBlock("Pending action confirmed", "No write has been executed.", draftAction)
            },
            Provenance = CreateProvenance(context, action),
            Detail = draftAction,
            SelectedAgentId = "pending-action-confirmation"
        };
    }

    private static AssistantAnswer CreateNoPendingAnswer(TilsoftExecutionContext context)
    {
        const string text = "There is no pending write preview to confirm in this conversation.";

        return new AssistantAnswer
        {
            AnswerType = "follow_up",
            Text = text,
            Blocks = new AnswerBlock[]
            {
                new FollowUpBlock(text, new[] { "Ask for a new write preview first." })
            },
            FollowUpQuestions = new[] { "Which write action would you like me to preview?" },
            Provenance = new AnswerProvenance
            {
                CapabilityKey = "action.confirmation",
                RowCount = 0,
                CorrelationId = context.CorrelationId
            },
            Detail = new Dictionary<string, object?>
            {
                ["reason"] = "no_pending_action"
            },
            SelectedAgentId = "pending-action-confirmation"
        };
    }

    private static AnswerProvenance CreateProvenance(TilsoftExecutionContext context, ActionRequestRecord action) => new()
    {
        CapabilityKey = string.IsNullOrWhiteSpace(action.CapabilityKey) ? "action.confirmation" : action.CapabilityKey,
        RowCount = 1,
        CorrelationId = string.IsNullOrWhiteSpace(action.CorrelationId) ? context.CorrelationId : action.CorrelationId,
        ProcedureName = action.ProposedProcedureName
    };

    private static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var value = input.Trim().ToLowerInvariant();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch == 'đ' ? 'd' : ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
