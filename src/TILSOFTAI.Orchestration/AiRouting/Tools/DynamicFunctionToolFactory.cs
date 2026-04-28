using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Execution;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Orchestration.AiRouting.Tools;

public sealed class DynamicFunctionToolFactory : IAgentFunctionToolFactory
{
    private readonly CapabilityToolDescriptionBuilder _descriptionBuilder;
    private readonly CapabilityParameterSchemaBuilder _schemaBuilder;
    private readonly ICapabilityExecutionFacade _executionFacade;
    private readonly ICompositeCapabilityExecutor _compositeExecutor;

    public DynamicFunctionToolFactory(
        CapabilityToolDescriptionBuilder descriptionBuilder,
        CapabilityParameterSchemaBuilder schemaBuilder,
        ICapabilityExecutionFacade executionFacade,
        ICompositeCapabilityExecutor compositeExecutor)
    {
        _descriptionBuilder = descriptionBuilder ?? throw new ArgumentNullException(nameof(descriptionBuilder));
        _schemaBuilder = schemaBuilder ?? throw new ArgumentNullException(nameof(schemaBuilder));
        _executionFacade = executionFacade ?? throw new ArgumentNullException(nameof(executionFacade));
        _compositeExecutor = compositeExecutor ?? throw new ArgumentNullException(nameof(compositeExecutor));
    }

    public Task<IReadOnlyList<AgentFunctionTool>> BuildToolsAsync(
        IReadOnlyList<CapabilityCandidate> candidates,
        TilsoftExecutionContext context,
        string locale,
        CancellationToken cancellationToken)
    {
        var tools = candidates
            .Where(candidate => IsModelCallable(candidate.Metadata))
            .Select(candidate => BuildTool(candidate.Metadata, context, locale))
            .ToArray();

        return Task.FromResult<IReadOnlyList<AgentFunctionTool>>(tools);
    }

    private AgentFunctionTool BuildTool(
        CapabilitySemanticMetadata capability,
        TilsoftExecutionContext context,
        string locale)
    {
        var schema = _schemaBuilder.Build(capability.Arguments, out var argumentMap);
        var name = CapabilityFunctionNameMapper.Map(capability.FunctionName, capability.CapabilityKey);

        return new AgentFunctionTool
        {
            Name = name,
            Capability = capability,
            Description = _descriptionBuilder.Build(capability),
            ParameterSchema = schema,
            Arguments = capability.Arguments,
            ModelToCapabilityArgumentMap = argumentMap,
            InvokeAsync = (modelFacingArguments, cancellationToken) =>
                InvokeCapabilityAsync(capability, argumentMap, modelFacingArguments, context, locale, cancellationToken)
        };
    }

    private async Task<CapabilityExecutionEnvelope> InvokeCapabilityAsync(
        CapabilitySemanticMetadata capability,
        IReadOnlyDictionary<string, string> argumentMap,
        IReadOnlyDictionary<string, object?> modelFacingArguments,
        TilsoftExecutionContext context,
        string locale,
        CancellationToken cancellationToken)
    {
        var capabilityArguments = TranslateArguments(argumentMap, modelFacingArguments);
        capabilityArguments["__capabilityKey"] = capability.CapabilityKey;
        capabilityArguments["__functionName"] = capability.FunctionName;
        capabilityArguments["__locale"] = locale;
        capabilityArguments["__correlationId"] = context.CorrelationId;

        if (IsReadMode(capability.ExecutionMode))
        {
            return await _executionFacade.ExecuteReadAsync(
                capability.CapabilityKey,
                capabilityArguments,
                cancellationToken);
        }

        if (IsWritePreviewMode(capability.ExecutionMode))
        {
            return await _executionFacade.PreviewWriteAsync(
                capability.CapabilityKey,
                capabilityArguments,
                cancellationToken);
        }

        if (capability.ExecutionMode.Equals("composite", StringComparison.OrdinalIgnoreCase))
        {
            return await _compositeExecutor.ExecuteAsync(
                capability.CapabilityKey,
                capabilityArguments,
                cancellationToken);
        }

        return CapabilityExecutionEnvelope.Blocked(
            capability.CapabilityKey,
            $"Unsupported execution mode: {capability.ExecutionMode}");
    }

    private static Dictionary<string, object?> TranslateArguments(
        IReadOnlyDictionary<string, string> argumentMap,
        IReadOnlyDictionary<string, object?> modelFacingArguments)
    {
        var translated = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (modelName, value) in modelFacingArguments)
        {
            translated[argumentMap.TryGetValue(modelName, out var capabilityName) ? capabilityName : modelName] = value;
        }

        return translated;
    }

    private static bool IsModelCallable(CapabilitySemanticMetadata capability) =>
        IsReadMode(capability.ExecutionMode)
        || IsWritePreviewMode(capability.ExecutionMode)
        || capability.ExecutionMode.Equals("composite", StringComparison.OrdinalIgnoreCase);

    private static bool IsReadMode(string executionMode) =>
        executionMode.Equals("read", StringComparison.OrdinalIgnoreCase)
        || executionMode.Equals("read_only", StringComparison.OrdinalIgnoreCase);

    private static bool IsWritePreviewMode(string executionMode) =>
        executionMode.Equals("write_preview", StringComparison.OrdinalIgnoreCase);
}
