using Microsoft.Extensions.Logging;
using TILSOFTAI.Agents.Abstractions;

namespace TILSOFTAI.Agents;

public sealed class DomainAgentRegistry : IAgentRegistry
{
    private readonly IReadOnlyList<IDomainAgent> _agents;
    private readonly Dictionary<string, IDomainAgent> _lookup;
    private readonly ILogger<DomainAgentRegistry> _logger;

    public DomainAgentRegistry(IEnumerable<IDomainAgent> agents, ILogger<DomainAgentRegistry> logger)
    {
        _agents = (agents ?? throw new ArgumentNullException(nameof(agents))).ToList();
        _lookup = _agents.ToDictionary(agent => agent.AgentId, StringComparer.OrdinalIgnoreCase);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogInformation(
            "AgentRegistryInitialized | AgentCount: {Count} | Agents: [{Agents}]",
            _agents.Count, string.Join(", ", _agents.Select(a => a.AgentId)));
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
            // Score: domain-specific agents first, legacy catch-all last
            candidates.Sort((a, b) =>
            {
                var aIsLegacy = string.Equals(a.AgentId, LegacyChatDomainAgent.AgentIdValue, StringComparison.OrdinalIgnoreCase);
                var bIsLegacy = string.Equals(b.AgentId, LegacyChatDomainAgent.AgentIdValue, StringComparison.OrdinalIgnoreCase);

                if (aIsLegacy && !bIsLegacy) return 1;  // legacy sorts last
                if (!aIsLegacy && bIsLegacy) return -1; // domain-specific sorts first

                // Among domain-specific agents, prefer exact domain match
                if (!string.IsNullOrWhiteSpace(task.DomainHint))
                {
                    var aOwns = a.OwnedDomains.Any(
                        d => string.Equals(d, task.DomainHint, StringComparison.OrdinalIgnoreCase));
                    var bOwns = b.OwnedDomains.Any(
                        d => string.Equals(d, task.DomainHint, StringComparison.OrdinalIgnoreCase));

                    if (aOwns && !bOwns) return -1;
                    if (!aOwns && bOwns) return 1;
                }

                return 0;
            });

            _logger.LogDebug(
                "AgentCandidatesResolved | DomainHint: {DomainHint} | Candidates: [{Candidates}]",
                task.DomainHint ?? "none",
                string.Join(", ", candidates.Select(c => c.AgentId)));

            return candidates;
        }

        // Fallback: use legacy agent if no domain agent matches
        if (_lookup.TryGetValue(LegacyChatDomainAgent.AgentIdValue, out var legacyAgent))
        {
            _logger.LogDebug(
                "AgentCandidatesFallback | DomainHint: {DomainHint} | Using: {AgentId}",
                task.DomainHint ?? "none", legacyAgent.AgentId);

            return new[] { legacyAgent };
        }

        _logger.LogWarning(
            "AgentCandidatesEmpty | DomainHint: {DomainHint} | No candidates found",
            task.DomainHint ?? "none");

        return Array.Empty<IDomainAgent>();
    }
}
