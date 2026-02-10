using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Prompting;

/// <summary>
/// PATCH 36.02: CompositeContextPackProvider now supports IScopedContextPackProvider.
/// When PromptBuildContext is available, providers that implement IScopedContextPackProvider
/// receive it for scoped data. Others fall back to the legacy interface.
/// </summary>
public sealed class CompositeContextPackProvider : IScopedContextPackProvider
{
    private readonly IReadOnlyList<IContextPackProvider> _providers;

    public CompositeContextPackProvider(IEnumerable<IContextPackProvider> providers)
    {
        _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
    }

    /// <summary>
    /// Legacy: no build context available — all providers use basic interface.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetContextPacksAsync(
        TilsoftExecutionContext context,
        CancellationToken cancellationToken)
    {
        return await GetContextPacksInternalAsync(context, buildContext: null, cancellationToken);
    }

    /// <summary>
    /// PATCH 36.02: With PromptBuildContext — scoped providers receive it.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetContextPacksAsync(
        TilsoftExecutionContext context,
        PromptBuildContext buildContext,
        CancellationToken cancellationToken)
    {
        return await GetContextPacksInternalAsync(context, buildContext, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, string>> GetContextPacksInternalAsync(
        TilsoftExecutionContext context,
        PromptBuildContext? buildContext,
        CancellationToken cancellationToken)
    {
        var combined = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in _providers)
        {
            IReadOnlyDictionary<string, string>? packs;

            // If build context is available and provider supports scoped interface, use it
            if (buildContext is not null && provider is IScopedContextPackProvider scopedProvider)
            {
                packs = await scopedProvider.GetContextPacksAsync(context, buildContext, cancellationToken);
            }
            else
            {
                packs = await provider.GetContextPacksAsync(context, cancellationToken);
            }

            if (packs == null)
            {
                continue;
            }

            foreach (var kvp in packs)
            {
                // Later providers overwrite earlier keys only if value is non-empty
                if (!string.IsNullOrWhiteSpace(kvp.Value))
                {
                    combined[kvp.Key] = kvp.Value;
                }
            }
        }

        return combined;
    }
}
