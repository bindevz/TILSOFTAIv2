namespace TILSOFTAI.Orchestration.Policies;

/// <summary>
/// Provides scoped ReAct follow-up rules from SQL.
/// Implementations must cache results per scope fingerprint.
/// </summary>
public interface IReActFollowUpRuleProvider
{
    Task<IReadOnlyList<ReActFollowUpRule>> GetScopedRulesAsync(
        string tenantId,
        IReadOnlyList<string> moduleKeys,
        string? appKey = null,
        CancellationToken ct = default);
}
