using System.Text.Json;
using Microsoft.Extensions.AI;
using TILSOFTAI.Orchestration.Execution;

namespace TILSOFTAI.Orchestration.AiRouting.Tools;

public sealed record OfficialAgentToolInvocation(
    AgentFunctionTool SourceTool,
    System.Text.Json.Nodes.JsonObject ModelFacingArguments,
    CapabilityExecutionEnvelope ToolResult);

public sealed record OfficialAgentTool(
    AITool Tool,
    AgentFunctionTool SourceTool);

public interface IOfficialAgentToolFactory
{
    IReadOnlyList<OfficialAgentTool> CreateTools(
        IReadOnlyList<AgentFunctionTool> tools,
        Action<OfficialAgentToolInvocation> onInvoked);
}

public sealed class OfficialAgentToolFactory : IOfficialAgentToolFactory
{
    public IReadOnlyList<OfficialAgentTool> CreateTools(
        IReadOnlyList<AgentFunctionTool> tools,
        Action<OfficialAgentToolInvocation> onInvoked)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(onInvoked);

        return tools
            .Select(tool => new OfficialAgentTool(
                new DescriptorBackedAIFunction(tool, onInvoked),
                tool))
            .ToArray();
    }

    private sealed class DescriptorBackedAIFunction : AIFunction
    {
        private readonly AgentFunctionTool _tool;
        private readonly Action<OfficialAgentToolInvocation> _onInvoked;
        private readonly JsonElement _jsonSchema;

        public DescriptorBackedAIFunction(
            AgentFunctionTool tool,
            Action<OfficialAgentToolInvocation> onInvoked)
        {
            _tool = tool ?? throw new ArgumentNullException(nameof(tool));
            _onInvoked = onInvoked ?? throw new ArgumentNullException(nameof(onInvoked));
            _jsonSchema = JsonSerializer.SerializeToElement(tool.Descriptor.ParameterSchema);
        }

        public override string Name => _tool.Descriptor.Name;

        public override string Description => _tool.Descriptor.Description;

        public override JsonElement JsonSchema => _jsonSchema;

        protected override async ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            var modelFacingArguments = arguments.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var toolResult = await _tool.InvokeAsync(modelFacingArguments, cancellationToken)
                .ConfigureAwait(false);

            _onInvoked(new OfficialAgentToolInvocation(
                _tool,
                JsonSerializer.SerializeToNode(modelFacingArguments)?.AsObject() ?? new(),
                toolResult));

            return toolResult;
        }
    }
}
