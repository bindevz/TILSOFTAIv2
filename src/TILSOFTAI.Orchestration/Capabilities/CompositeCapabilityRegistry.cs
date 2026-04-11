using Microsoft.Extensions.Logging;

namespace TILSOFTAI.Orchestration.Capabilities;

/// <summary>
/// Sprint 5: Composite capability registry that loads from multiple ICapabilitySource instances.
/// Replaces direct InMemoryCapabilityRegistry usage in production.
/// InMemoryCapabilityRegistry remains available as test fixture / fallback.
///
/// Loading order:
///   1. Static fallback capabilities (WarehouseCapabilities, AccountingCapabilities)
///   2. Data-driven sources (configuration, SQL — future)
///   3. Data-driven sources override static entries with the same CapabilityKey
/// </summary>
public sealed class CompositeCapabilityRegistry : ICapabilityRegistry
{
    private readonly Dictionary<string, CapabilityDescriptor> _byKey;
    private readonly Dictionary<string, List<CapabilityDescriptor>> _byDomain;

    public CompositeCapabilityRegistry(
        IEnumerable<ICapabilitySource> sources,
        ILogger<CompositeCapabilityRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var log = logger ?? throw new ArgumentNullException(nameof(logger));

        _byKey = new Dictionary<string, CapabilityDescriptor>(StringComparer.OrdinalIgnoreCase);
        _byDomain = new Dictionary<string, List<CapabilityDescriptor>>(StringComparer.OrdinalIgnoreCase);

        var totalLoaded = 0;

        foreach (var source in sources)
        {
            var capabilities = source.Load();
            log.LogInformation(
                "CompositeCapabilityRegistry | Source: {SourceName} | Loaded: {Count}",
                source.SourceName, capabilities.Count);

            foreach (var cap in capabilities)
            {
                _byKey[cap.CapabilityKey] = cap; // later sources override earlier entries
                totalLoaded++;
            }
        }

        // Build domain index
        foreach (var cap in _byKey.Values)
        {
            if (!_byDomain.TryGetValue(cap.Domain, out var domainList))
            {
                domainList = new List<CapabilityDescriptor>();
                _byDomain[cap.Domain] = domainList;
            }

            domainList.Add(cap);
        }

        log.LogInformation(
            "CompositeCapabilityRegistry | TotalCapabilities: {Total} | Domains: [{Domains}]",
            _byKey.Count,
            string.Join(", ", _byDomain.Keys));
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
