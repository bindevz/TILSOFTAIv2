using Microsoft.Extensions.Logging;
using TILSOFTAI.Agents.Abstractions;

namespace TILSOFTAI.Agents.Domain;

/// <summary>
/// Abstract base for domain agent skeletons.
/// Default execution is deterministic and native-first; domain agents own their execution paths.
/// </summary>
public abstract class DomainAgentBase : IDomainAgent
{
    private readonly ILogger _logger;

    protected ILogger Logger => _logger;

    protected DomainAgentBase(ILogger logger)
    {
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

    public virtual async Task<AgentResult> ExecuteAsync(AgentTask task, AgentExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogInformation(
            "AgentExecution | AgentId: {AgentId} | IntentType: {IntentType} | DomainHint: {DomainHint}",
            AgentId, task.IntentType, task.DomainHint ?? "none");

        // Sprint 3: enforce write governance before delegation
        AgentWritePolicy.EnforceWriteGovernance(task, AgentId, _logger);

        await Task.CompletedTask;

        _logger.LogInformation(
            "AgentExecutionCompleted | AgentId: {AgentId} | Success: false | Reason: native_path_not_implemented",
            AgentId);

        return AgentResult.Fail(
            "This domain agent does not have a native execution path for the request.",
            "DOMAIN_NATIVE_PATH_NOT_IMPLEMENTED",
            new { agentId = AgentId });
    }
}
