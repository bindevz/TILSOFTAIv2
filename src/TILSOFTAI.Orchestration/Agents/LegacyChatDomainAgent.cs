using System.Diagnostics;
using TILSOFTAI.Agents.Abstractions;
using TILSOFTAI.Orchestration.Observability;

namespace TILSOFTAI.Agents;

/// <summary>
/// Legacy catch-all domain agent. Handles requests that no specialized domain agent claims.
/// Sprint 2: reduced priority — only matches when DomainHint is null/empty or explicitly "legacy-chat".
/// Delegates to LegacyChatPipelineBridge (shared with other domain agents).
///
/// TRANSITIONAL COMPONENT — Sprint 5 compatibility debt:
///   Why it still exists: Catch-all for unclassified requests. No domain agent claims requests with null/empty DomainHint.
///   What depends on it: DomainAgentRegistry fallback logic, any request that fails intent classification.
///   What removes it: Sprint 6+ — when intent classification covers all production domains,
///     or when a default "general" agent replaces this catch-all pattern.
/// </summary>
public sealed class LegacyChatDomainAgent : IDomainAgent
{
    public const string AgentIdValue = "legacy-chat";

    private readonly LegacyChatPipelineBridge _bridge;
    private readonly RuntimeExecutionInstrumentation? _instrumentation;

    public LegacyChatDomainAgent(
        LegacyChatPipelineBridge bridge,
        RuntimeExecutionInstrumentation? instrumentation = null)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _instrumentation = instrumentation;
    }

    public string AgentId => AgentIdValue;

    public string DisplayName => "Legacy Chat Domain Agent";

    public IReadOnlyList<string> OwnedDomains { get; } = new[] { "legacy-chat", "cross-domain" };

    public bool CanHandle(AgentTask task)
    {
        if (task is null)
        {
            return false;
        }

        // Sprint 2 priority reduction:
        // Only claim the task if no domain hint is set (catch-all), or if explicitly targeted
        if (string.IsNullOrWhiteSpace(task.DomainHint))
        {
            return true; // catch-all for unclassified requests
        }

        return string.Equals(task.DomainHint, "legacy-chat", StringComparison.OrdinalIgnoreCase)
            || string.Equals(task.DomainHint, "cross-domain", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<AgentResult> ExecuteAsync(AgentTask task, AgentExecutionContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var result = await _bridge.ExecuteAsync(task, context, ct);
        sw.Stop();
        _instrumentation?.RecordBridgeFallback(AgentId, "legacy_agent", sw.Elapsed, result.Success);
        return result;
    }
}
