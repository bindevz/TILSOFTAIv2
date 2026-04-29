using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Execution;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Orchestration.AiRouting.Tools;

public sealed class DynamicFunctionToolFactory : IOfficialAgentFunctionProvider
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

    public Task<IReadOnlyList<AIFunction>> BuildFunctionsAsync(
        IReadOnlyList<CapabilityCandidate> candidates,
        TilsoftExecutionContext context,
        string locale,
        CancellationToken cancellationToken)
    {
        var tools = candidates
            .Where(candidate => IsModelCallable(candidate.Metadata, _options))
            .Select(candidate => BuildFunction(candidate.Metadata, context, locale))
            .Take(EffectiveMaxCandidateTools(_options))
            .ToArray();

        return Task.FromResult<IReadOnlyList<AIFunction>>(tools);
    }

    private AIFunction BuildFunction(
        CapabilitySemanticMetadata capability,
        TilsoftExecutionContext context,
        string locale)
    {
        var descriptor = _descriptorFactory.Create(capability);

        return new DescriptorBackedAIFunction(
            descriptor,
            (modelFacingArguments, cancellationToken) =>
                InvokeCapabilityAsync(
                    descriptor.Capability,
                    descriptor.ModelToCapabilityArgumentMap,
                    modelFacingArguments,
                    context,
                    locale,
                    cancellationToken));
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

    private static bool IsModelCallable(CapabilitySemanticMetadata capability, AiRoutingOptions options) =>
        DomainGate.IsRuntimeAllowedDomain(capability.Domain)
        && (IsReadMode(capability.ExecutionMode)
            || options.EnableWritePreviewTools && IsWritePreviewMode(capability.ExecutionMode)
            || capability.ExecutionMode.Equals("composite", StringComparison.OrdinalIgnoreCase));

    private static int EffectiveMaxCandidateTools(AiRoutingOptions options)
    {
        var maxCandidateTools = options.MaxCandidateTools > 0
            ? options.MaxCandidateTools
            : options.MaxTotalCandidateTools;
        return Math.Max(0, Math.Min(maxCandidateTools, options.MaxTotalCandidateTools));
    }

    private static bool IsReadMode(string executionMode) =>
        executionMode.Equals("read", StringComparison.OrdinalIgnoreCase)
        || executionMode.Equals("read_only", StringComparison.OrdinalIgnoreCase);

    private static bool IsWritePreviewMode(string executionMode) =>
        executionMode.Equals("write_preview", StringComparison.OrdinalIgnoreCase);

    private sealed class DescriptorBackedAIFunction : AIFunction, ICapabilityBackedAIFunction
    {
        private readonly Func<IReadOnlyDictionary<string, object?>, CancellationToken, Task<CapabilityExecutionEnvelope>> _invokeAsync;
        private readonly JsonElement _jsonSchema;

        public DescriptorBackedAIFunction(
            CapabilityToolDescriptor descriptor,
            Func<IReadOnlyDictionary<string, object?>, CancellationToken, Task<CapabilityExecutionEnvelope>> invokeAsync)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            _invokeAsync = invokeAsync ?? throw new ArgumentNullException(nameof(invokeAsync));
            _jsonSchema = JsonSerializer.SerializeToElement(descriptor.ParameterSchema);
        }

        public CapabilityToolDescriptor Descriptor { get; }

        public OfficialAgentFunctionInvocation? LastInvocation { get; private set; }

        public override string Name => Descriptor.Name;

        public override string Description => Descriptor.Description;

        public override JsonElement JsonSchema => _jsonSchema;

        protected override async ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            var modelFacingArguments = arguments.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var toolResult = await _invokeAsync(modelFacingArguments, cancellationToken)
                .ConfigureAwait(false);

            LastInvocation = new OfficialAgentFunctionInvocation(
                Descriptor,
                JsonSerializer.SerializeToNode(modelFacingArguments)?.AsObject() ?? new(),
                toolResult);

            return toolResult;
        }
    }
}
