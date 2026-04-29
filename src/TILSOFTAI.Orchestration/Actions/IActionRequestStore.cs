namespace TILSOFTAI.Orchestration.Actions;

public interface IActionRequestStore
{
    Task<ActionRequestRecord> CreateAsync(ActionRequestCreateRequest request, CancellationToken cancellationToken);
    Task<ActionRequestRecord?> GetAsync(string tenantId, string actionId, CancellationToken cancellationToken);
    Task<ActionRequestRecord?> GetActiveForConversationAsync(
        string tenantId,
        string userId,
        string conversationId,
        CancellationToken cancellationToken);
    Task<ActionRequestRecord> ConfirmAsync(string tenantId, string userId, string actionId, CancellationToken cancellationToken);
    Task<ActionRequestRecord> RejectAsync(
        string tenantId,
        string userId,
        string actionId,
        string? reason,
        CancellationToken cancellationToken);
    Task<ActionRequestRecord> MarkExecutedAsync(
        string tenantId,
        string actionId,
        string executedByUserId,
        string? resultCompactJson,
        bool success,
        CancellationToken cancellationToken);
    Task<int> ExpireOldAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken);
}
