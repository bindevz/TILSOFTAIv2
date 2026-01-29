using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Prompting;

public sealed class CompositeContextPackProvider : IContextPackProvider
{
    private readonly IReadOnlyList<IContextPackProvider> _providers;

    public CompositeContextPackProvider(IEnumerable<IContextPackProvider> providers)
    {
        _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
    }

    public async Task<IReadOnlyDictionary<string, string>> GetContextPacksAsync(
        TilsoftExecutionContext context,
        CancellationToken cancellationToken)
    {
        var combined = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in _providers)
        {
            var packs = await provider.GetContextPacksAsync(context, cancellationToken);
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
