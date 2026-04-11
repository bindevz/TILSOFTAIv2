namespace TILSOFTAI.Orchestration.Capabilities;

/// <summary>
/// Sprint 5: Wraps a static IReadOnlyList&lt;CapabilityDescriptor&gt; as an ICapabilitySource.
/// Used to register WarehouseCapabilities.All and AccountingCapabilities.All as sources
/// for the CompositeCapabilityRegistry, keeping static definitions as fallbacks.
/// </summary>
public sealed class StaticCapabilitySource : ICapabilitySource
{
    private readonly IReadOnlyList<CapabilityDescriptor> _capabilities;

    public StaticCapabilitySource(string sourceName, IReadOnlyList<CapabilityDescriptor> capabilities)
    {
        SourceName = sourceName ?? throw new ArgumentNullException(nameof(sourceName));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
    }

    public string SourceName { get; }

    public IReadOnlyList<CapabilityDescriptor> Load() => _capabilities;
}
