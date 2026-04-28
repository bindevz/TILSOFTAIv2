using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Execution;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Orchestration.AiRouting.Tools;

public sealed record OfficialAgentFunctionInvocation(
    CapabilityToolDescriptor Descriptor,
    JsonObject ModelFacingArguments,
    CapabilityExecutionEnvelope ToolResult);

public interface ICapabilityBackedAIFunction
{
    CapabilityToolDescriptor Descriptor { get; }
    OfficialAgentFunctionInvocation? LastInvocation { get; }
}

public interface IOfficialAgentFunctionProvider
{
    Task<IReadOnlyList<AIFunction>> BuildFunctionsAsync(
        IReadOnlyList<CapabilityCandidate> candidates,
        TilsoftExecutionContext context,
        string locale,
        CancellationToken cancellationToken);
}
