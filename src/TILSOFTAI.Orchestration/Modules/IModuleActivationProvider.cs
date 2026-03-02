namespace TILSOFTAI.Orchestration.Modules;

/// <summary>
/// PATCH 37.01: DB-driven module activation provider.
/// Returns enabled module assembly names for a given tenant/environment.
/// </summary>
public interface IModuleActivationProvider
{
    /// <summary>
    /// Get the list of enabled module assembly names from the database.
    /// </summary>
    Task<IReadOnlyList<string>> GetEnabledModulesAsync(
        string? tenantId = null,
        string? environment = null,
        CancellationToken ct = default);
}
