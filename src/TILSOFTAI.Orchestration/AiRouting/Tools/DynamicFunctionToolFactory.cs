using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Execution;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Orchestration.AiRouting.Tools;

public sealed class DynamicFunctionToolFactory : IAgentFunctionToolFactory
{
    private readonly ICapabilityToolDescriptorFactory _descriptorFactory;
    private readonly ICapabilityExecutionFacade _executionFacade;
    private readonly ICompositeCapabilityExecutor _compositeExecutor;
    private readonly AiRoutingOptions _options;

    public DynamicFunctionToolFactory(
        ICapabilityToolDescriptorFactory descriptorFactory,
        ICapabilityExecutionFacade executionFacade,
        ICompositeCapabilityExecutor compositeExecutor,
        IOptions<AiRoutingOptions>? options = null)
    {
        _descriptorFactory = descriptorFactory ?? throw new ArgumentNullException(nameof(descriptorFactory));
        _executionFacade = executionFacade ?? throw new ArgumentNullException(nameof(executionFacade));
        _compositeExecutor = compositeExecutor ?? throw new ArgumentNullException(nameof(compositeExecutor));
        _options = options?.Value ?? new AiRoutingOptions();
    }

    public Task<IReadOnlyList<AgentFunctionTool>> BuildToolsAsync(
        IReadOnlyList<CapabilityCandidate> candidates,
        TilsoftExecutionContext context,
        string locale,
        CancellationToken cancellationToken)
    {
        var tools = candidates
            .Where(candidate => IsModelCallable(candidate.Metadata, _options))
            .Select(candidate => BuildTool(candidate.Metadata, context, locale))
            .ToArray();

        return Task.FromResult<IReadOnlyList<AgentFunctionTool>>(tools);
    }

    private AgentFunctionTool BuildTool(
        CapabilitySemanticMetadata capability,
        TilsoftExecutionContext context,
        string locale)
    {
        var descriptor = _descriptorFactory.Create(capability);

        return new AgentFunctionTool
        {
            Descriptor = descriptor,
            InvokeAsync = (modelFacingArguments, cancellationToken) =>
                InvokeCapabilityAsync(
                    descriptor.Capability,
                    descriptor.ModelToCapabilityArgumentMap,
                    modelFacingArguments,
                    context,
                    locale,
                    cancellationToken)
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

        if (IsMutationMode(capability.ExecutionMode))
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

    private static bool IsModelCallable(CapabilitySemanticMetadata capability, AiRoutingOptions options) =>
        IsReadMode(capability.ExecutionMode)
        || options.EnableWritePreviewTools && IsMutationMode(capability.ExecutionMode)
        || capability.ExecutionMode.Equals("composite", StringComparison.OrdinalIgnoreCase);

    private static bool IsReadMode(string executionMode) =>
        executionMode.Equals("read", StringComparison.OrdinalIgnoreCase)
        || executionMode.Equals("read_only", StringComparison.OrdinalIgnoreCase);

    private static bool IsWritePreviewMode(string executionMode) =>
        executionMode.Equals("write_preview", StringComparison.OrdinalIgnoreCase);

    private static bool IsApprovedWriteMode(string executionMode) =>
        executionMode.Equals("write", StringComparison.OrdinalIgnoreCase)
        || executionMode.Equals("approved_write", StringComparison.OrdinalIgnoreCase)
        || executionMode.Equals("execute_write", StringComparison.OrdinalIgnoreCase)
        || executionMode.Equals("write_execute", StringComparison.OrdinalIgnoreCase);

    private static bool IsMutationMode(string executionMode) =>
        IsWritePreviewMode(executionMode) || IsApprovedWriteMode(executionMode);
}
