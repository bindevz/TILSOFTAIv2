namespace TILSOFTAI.Orchestration.Capabilities;

/// <summary>
/// Sprint 4: In-memory capability registry seeded at startup.
/// Future sprints may replace with a data-driven (SQL/config) registry.
/// </summary>
public sealed class InMemoryCapabilityRegistry : ICapabilityRegistry
{
    private readonly Dictionary<string, CapabilityDescriptor> _byKey;
    private readonly Dictionary<string, List<CapabilityDescriptor>> _byDomain;

    public InMemoryCapabilityRegistry(IEnumerable<CapabilityDescriptor> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        var list = capabilities.ToList();
        _byKey = new Dictionary<string, CapabilityDescriptor>(StringComparer.OrdinalIgnoreCase);
        _byDomain = new Dictionary<string, List<CapabilityDescriptor>>(StringComparer.OrdinalIgnoreCase);

        foreach (var cap in list)
        {
            _byKey[cap.CapabilityKey] = cap;

            if (!_byDomain.TryGetValue(cap.Domain, out var domainList))
            {
                domainList = new List<CapabilityDescriptor>();
                _byDomain[cap.Domain] = domainList;
            }

            domainList.Add(cap);
        }
    }

    public IReadOnlyList<CapabilityDescriptor> GetByDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return Array.Empty<CapabilityDescriptor>();
        }

        return _byDomain.TryGetValue(domain, out var list)
            ? list.AsReadOnly()
            : Array.Empty<CapabilityDescriptor>();
    }

    public CapabilityDescriptor? Resolve(string capabilityKey)
    {
        if (string.IsNullOrWhiteSpace(capabilityKey))
        {
            return null;
        }

        return _byKey.TryGetValue(capabilityKey, out var cap) ? cap : null;
    }
}
