namespace TILSOFTAI.Orchestration.Actions;

public interface IActionRequestStore
{
    Task<ActionRequestRecord> CreateAsync(ActionRequestRecord request, CancellationToken cancellationToken);
    Task<ActionRequestRecord?> GetAsync(string tenantId, string actionId, CancellationToken cancellationToken);
    Task<ActionRequestRecord> ApproveAsync(string tenantId, string actionId, string approvedByUserId, CancellationToken cancellationToken);
    Task<ActionRequestRecord> RejectAsync(string tenantId, string actionId, string approvedByUserId, CancellationToken cancellationToken);
    Task<ActionRequestRecord> MarkExecutedAsync(string tenantId, string actionId, string resultCompactJson, bool success, CancellationToken cancellationToken);
}
