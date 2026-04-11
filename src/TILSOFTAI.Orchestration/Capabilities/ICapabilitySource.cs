namespace TILSOFTAI.Orchestration.Capabilities;

/// <summary>
/// Sprint 5: Abstraction for data-driven capability sources.
/// Implementations can load capabilities from configuration, SQL, or other sources.
/// </summary>
public interface ICapabilitySource
{
    /// <summary>
    /// Unique name identifying this source (e.g. "configuration", "sql", "static-warehouse").
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Load capability descriptors from this source.
    /// Called at startup to populate the capability registry.
    /// </summary>
    IReadOnlyList<CapabilityDescriptor> Load();
}
