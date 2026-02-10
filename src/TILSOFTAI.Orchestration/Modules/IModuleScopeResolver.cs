using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Modules;

public interface IModuleScopeResolver
{
    Task<ModuleScopeResult> ResolveAsync(
        string userQuery,
        TilsoftExecutionContext context,
        CancellationToken cancellationToken);
}
