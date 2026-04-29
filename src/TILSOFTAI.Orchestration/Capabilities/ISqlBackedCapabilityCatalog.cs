namespace TILSOFTAI.Orchestration.Capabilities;

public interface ISqlBackedCapabilityCatalog
{
    Task<IReadOnlyList<CapabilityDescriptor>> GetEnabledCapabilitiesAsync(
        IReadOnlySet<string> allowedDomains,
        CancellationToken ct);

    Task<CapabilityDescriptor?> GetByKeyAsync(
        string capabilityKey,
        CancellationToken ct);

    Task<IReadOnlyList<CapabilityDescriptor>> GetByDomainAsync(
        string domain,
        CancellationToken ct);
}

public interface ICapabilityCatalogReloader
{
    Task ReloadAsync(CancellationToken ct);
}
