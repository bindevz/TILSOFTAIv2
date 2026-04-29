using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Prompting;

/// <summary>
/// Extended context pack provider interface that accepts
/// PromptBuildContext for scoped data. Providers that need scoped tools
/// or module-aware filtering implement this instead of relying on mutable state.
/// </summary>
public interface IScopedContextPackProvider : IContextPackProvider
{
    Task<IReadOnlyDictionary<string, string>> GetContextPacksAsync(
        TilsoftExecutionContext context,
        PromptBuildContext buildContext,
        CancellationToken cancellationToken);
}
