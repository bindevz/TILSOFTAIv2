using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Normalization;

public interface INormalizationService
{
    Task<string> NormalizeAsync(string input, TilsoftExecutionContext context, CancellationToken ct);
}
