using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Modules;

[Obsolete("Module-centric scope resolution is deprecated in Sprint 1. Migrate routing to supervisor and domain agents.")]
public interface IModuleScopeResolver
{
    Task<ModuleScopeResult> ResolveAsync(
        string userQuery,
        TilsoftExecutionContext context,
        CancellationToken cancellationToken);
}
