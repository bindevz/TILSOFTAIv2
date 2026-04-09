namespace TILSOFTAI.Agents.Abstractions;

public interface IAgentRegistry
{
    IReadOnlyList<IDomainAgent> GetAll();
    IDomainAgent? GetById(string agentId);
    IReadOnlyList<IDomainAgent> ResolveCandidates(AgentTask task);
}
