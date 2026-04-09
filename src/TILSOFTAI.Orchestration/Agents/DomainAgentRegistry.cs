using TILSOFTAI.Agents.Abstractions;

namespace TILSOFTAI.Agents;

public sealed class DomainAgentRegistry : IAgentRegistry
{
    private readonly IReadOnlyList<IDomainAgent> _agents;
    private readonly Dictionary<string, IDomainAgent> _lookup;

    public DomainAgentRegistry(IEnumerable<IDomainAgent> agents)
    {
        _agents = (agents ?? throw new ArgumentNullException(nameof(agents))).ToList();
        _lookup = _agents.ToDictionary(agent => agent.AgentId, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IDomainAgent> GetAll() => _agents;

    public IDomainAgent? GetById(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return null;
        }

        return _lookup.TryGetValue(agentId, out var agent) ? agent : null;
    }

    public IReadOnlyList<IDomainAgent> ResolveCandidates(AgentTask task)
    {
        if (task is null)
        {
            return Array.Empty<IDomainAgent>();
        }

        var candidates = _agents.Where(agent => agent.CanHandle(task)).ToList();
        if (candidates.Count > 0)
        {
            return candidates;
        }

        if (_lookup.TryGetValue(LegacyChatDomainAgent.AgentIdValue, out var legacyAgent))
        {
            return new[] { legacyAgent };
        }

        return Array.Empty<IDomainAgent>();
    }
}
