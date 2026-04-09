using Microsoft.Extensions.Logging;
using TILSOFTAI.Agents.Abstractions;

namespace TILSOFTAI.Agents.Domain;

/// <summary>
/// Abstract base for Sprint 2 domain agent skeletons.
/// Delegates execution to LegacyChatPipelineBridge.
/// Subclasses define identity (AgentId, OwnedDomains) and CanHandle routing.
/// </summary>
public abstract class DomainAgentBase : IDomainAgent
{
    private readonly LegacyChatPipelineBridge _bridge;
    private readonly ILogger _logger;

    protected DomainAgentBase(LegacyChatPipelineBridge bridge, ILogger logger)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public abstract string AgentId { get; }
    public abstract string DisplayName { get; }
    public abstract IReadOnlyList<string> OwnedDomains { get; }

    public virtual bool CanHandle(AgentTask task)
    {
        if (task is null)
        {
            return false;
        }

        // Match if the task's domain hint is one of this agent's owned domains
        if (!string.IsNullOrWhiteSpace(task.DomainHint))
        {
            return OwnedDomains.Any(
                d => string.Equals(d, task.DomainHint, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    public async Task<AgentResult> ExecuteAsync(AgentTask task, AgentExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogInformation(
            "AgentExecution | AgentId: {AgentId} | IntentType: {IntentType} | DomainHint: {DomainHint}",
            AgentId, task.IntentType, task.DomainHint ?? "none");

        // Sprint 3: enforce write governance before delegation
        AgentWritePolicy.EnforceWriteGovernance(task, AgentId, _logger);

        // Delegate to legacy bridge; future sprints will add domain-specific logic
        var result = await _bridge.ExecuteAsync(task, context, ct);

        _logger.LogInformation(
            "AgentExecutionCompleted | AgentId: {AgentId} | Success: {Success}",
            AgentId, result.Success);

        return result;
    }
}
