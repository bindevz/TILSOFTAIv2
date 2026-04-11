namespace TILSOFTAI.Orchestration.Capabilities;

/// <summary>
/// Sprint 4: Minimal capability registry for runtime capability resolution.
/// Agents use this to find capabilities by domain or key, then execute via ToolAdapterRegistry.
/// </summary>
public interface ICapabilityRegistry
{
    /// <summary>
    /// Returns all registered capabilities.
    /// </summary>
    IReadOnlyList<CapabilityDescriptor> GetAll();

    /// <summary>
    /// Returns all capabilities registered for the given domain.
    /// </summary>
    IReadOnlyList<CapabilityDescriptor> GetByDomain(string domain);

    /// <summary>
    /// Resolves a single capability by its unique key.
    /// Returns null if the key is not registered.
    /// </summary>
    CapabilityDescriptor? Resolve(string capabilityKey);
}
