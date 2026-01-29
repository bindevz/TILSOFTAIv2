using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Prompting;

public interface IContextPackProvider
{
    Task<IReadOnlyDictionary<string, string>> GetContextPacksAsync(TilsoftExecutionContext context, CancellationToken cancellationToken);
}
