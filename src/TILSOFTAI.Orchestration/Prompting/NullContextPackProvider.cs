using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Prompting;

public sealed class NullContextPackProvider : IContextPackProvider
{
    public Task<IReadOnlyDictionary<string, string>> GetContextPacksAsync(TilsoftExecutionContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }
}
