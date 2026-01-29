using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Orchestration.Caching;

public interface ISemanticCache
{
    bool Enabled { get; }

    Task<string?> TryGetAnswerAsync(
        TilsoftExecutionContext context,
        string module,
        string question,
        IReadOnlyList<ToolDefinition> tools,
        string? planJson,
        bool containsSensitive,
        CancellationToken ct);

    Task SetAnswerAsync(
        TilsoftExecutionContext context,
        string module,
        string question,
        IReadOnlyList<ToolDefinition> tools,
        string? planJson,
        string answer,
        bool containsSensitive,
        CancellationToken ct);
}
