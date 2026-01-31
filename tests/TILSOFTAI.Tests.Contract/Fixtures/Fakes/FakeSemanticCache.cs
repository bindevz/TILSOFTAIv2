using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Caching;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Tests.Contract.Fixtures.Fakes;

/// <summary>
/// Fake ISemanticCache for contract tests that simulates cache misses.
/// </summary>
public sealed class FakeSemanticCache : ISemanticCache
{
    public bool Enabled => false;

    public Task<string?> TryGetAnswerAsync(TilsoftExecutionContext context, string module, string question, IReadOnlyList<ToolDefinition> tools, string? planJson, bool containsSensitive, CancellationToken ct)
    {
        // Always return null (cache miss) in tests
        return Task.FromResult<string?>(null);
    }

    public Task SetAnswerAsync(TilsoftExecutionContext context, string module, string question, IReadOnlyList<ToolDefinition> tools, string? planJson, string answer, bool containsSensitive, CancellationToken ct)
    {
        // No-op: do not persist cache in tests
        return Task.CompletedTask;
    }
}
