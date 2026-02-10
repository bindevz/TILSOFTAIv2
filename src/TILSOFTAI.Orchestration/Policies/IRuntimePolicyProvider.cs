namespace TILSOFTAI.Orchestration.Policies;

/// <summary>
/// Resolves runtime policies from SQL for a given scope (tenant, modules, app, env, language).
/// Implementations must cache results per scope fingerprint.
/// </summary>
public interface IRuntimePolicyProvider
{
    Task<RuntimePolicySnapshot> ResolveAsync(
        string tenantId,
        IReadOnlyList<string> moduleKeys,
        string? appKey = null,
        string? environment = null,
        string? language = null,
        CancellationToken ct = default);
}
