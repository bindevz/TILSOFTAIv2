namespace TILSOFTAI.Orchestration.Capabilities;

/// <summary>
/// Sprint 5: Contract for structured capability resolution.
/// Replaces per-agent string-matching with a reusable resolution mechanism.
/// Used by domain agents to select the correct capability from candidates.
/// </summary>
public interface ICapabilityResolver
{
    /// <summary>
    /// Resolve a capability from the given candidates using the structured hint.
    /// Returns null if no capability matches the hint.
    /// </summary>
    /// <param name="hint">Structured capability request hint from supervisor.</param>
    /// <param name="candidates">Capabilities available for the target domain.</param>
    /// <returns>The best-matching capability, or null if no match is found.</returns>
    CapabilityDescriptor? Resolve(CapabilityRequestHint hint, IReadOnlyList<CapabilityDescriptor> candidates);
}
