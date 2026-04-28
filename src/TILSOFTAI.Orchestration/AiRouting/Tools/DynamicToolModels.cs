using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Execution;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Orchestration.AiRouting.Tools;

public sealed record AgentFunctionTool
{
    public required CapabilityToolDescriptor Descriptor { get; init; }
    public required Func<IReadOnlyDictionary<string, object?>, CancellationToken, Task<CapabilityExecutionEnvelope>> InvokeAsync { get; init; }

    public string Name => Descriptor.Name;
    public CapabilitySemanticMetadata Capability => Descriptor.Capability;
    public string Description => Descriptor.Description;
    public System.Text.Json.Nodes.JsonObject ParameterSchema => Descriptor.ParameterSchema;
    public IReadOnlyList<CapabilityArgumentMetadata> Arguments => Descriptor.Arguments;
    public IReadOnlyDictionary<string, string> ModelToCapabilityArgumentMap => Descriptor.ModelToCapabilityArgumentMap;
}

public interface IAgentFunctionToolFactory
{
    Task<IReadOnlyList<AgentFunctionTool>> BuildToolsAsync(
        IReadOnlyList<CapabilityCandidate> candidates,
        TilsoftExecutionContext context,
        string locale,
        CancellationToken cancellationToken);
}
