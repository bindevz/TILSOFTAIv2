using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Orchestration.Capabilities;

/// <summary>
/// Represents a cohesive pack of capabilities (tools) that can be loaded into the runtime.
/// Sprint 2: wraps existing ITilsoftModule. Future sprints replace with data-driven packs.
/// </summary>
public interface ICapabilityPackProvider
{
    /// <summary>
    /// Unique name for this capability pack.
    /// </summary>
    string PackName { get; }

    /// <summary>
    /// Domains that this pack's tools are related to.
    /// Used for informational/routing purposes.
    /// </summary>
    IReadOnlyList<string> DomainAffinity { get; }

    /// <summary>
    /// Register tools and handlers from this pack into the runtime registries.
    /// </summary>
    void RegisterTools(IToolRegistry registry, INamedToolHandlerRegistry handlerRegistry);
}
