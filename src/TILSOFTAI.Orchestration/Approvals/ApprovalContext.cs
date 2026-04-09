using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Approvals;

public sealed class ApprovalContext
{
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public string ConversationId { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public string? AgentId { get; init; }

    public static ApprovalContext FromExecutionContext(TilsoftExecutionContext context, string? agentId = null) => new()
    {
        TenantId = context?.TenantId ?? string.Empty,
        UserId = context?.UserId ?? string.Empty,
        Roles = context?.Roles ?? Array.Empty<string>(),
        ConversationId = context?.ConversationId ?? string.Empty,
        CorrelationId = context?.CorrelationId ?? string.Empty,
        AgentId = agentId
    };
}
