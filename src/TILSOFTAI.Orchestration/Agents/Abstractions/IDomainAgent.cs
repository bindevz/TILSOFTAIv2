namespace TILSOFTAI.Agents.Abstractions;

public interface IDomainAgent
{
    string AgentId { get; }
    string DisplayName { get; }
    IReadOnlyList<string> OwnedDomains { get; }

    bool CanHandle(AgentTask task);

    Task<AgentResult> ExecuteAsync(AgentTask task, AgentExecutionContext context, CancellationToken ct);
}
