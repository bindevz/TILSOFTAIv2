using System.Text.Json.Nodes;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Execution;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Orchestration.AiRouting.Tools;

public sealed record AgentFunctionTool
{
    public required string Name { get; init; }
    public required CapabilitySemanticMetadata Capability { get; init; }
    public required string Description { get; init; }
    public required JsonObject ParameterSchema { get; init; }
    public IReadOnlyList<CapabilityArgumentMetadata> Arguments { get; init; } = Array.Empty<CapabilityArgumentMetadata>();
    public IReadOnlyDictionary<string, string> ModelToCapabilityArgumentMap { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public required Func<IReadOnlyDictionary<string, object?>, CancellationToken, Task<CapabilityExecutionEnvelope>> InvokeAsync { get; init; }
}

public interface IAgentFunctionToolFactory
{
    Task<IReadOnlyList<AgentFunctionTool>> BuildToolsAsync(
        IReadOnlyList<CapabilityCandidate> candidates,
        TilsoftExecutionContext context,
        string locale,
        CancellationToken cancellationToken);
}
