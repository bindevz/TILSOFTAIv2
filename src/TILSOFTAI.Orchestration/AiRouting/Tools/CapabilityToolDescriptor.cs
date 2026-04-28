using System.Text.Json.Nodes;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Orchestration.AiRouting.Tools;

public sealed record CapabilityToolDescriptor
{
    public required string Name { get; init; }
    public required CapabilitySemanticMetadata Capability { get; init; }
    public required string Description { get; init; }
    public required JsonObject ParameterSchema { get; init; }
    public IReadOnlyList<CapabilityArgumentMetadata> Arguments { get; init; } = Array.Empty<CapabilityArgumentMetadata>();
    public IReadOnlyDictionary<string, string> ModelToCapabilityArgumentMap { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public interface ICapabilityToolDescriptorFactory
{
    CapabilityToolDescriptor Create(CapabilitySemanticMetadata capability);
}

public sealed class CapabilityToolDescriptorFactory : ICapabilityToolDescriptorFactory
{
    private readonly CapabilityToolDescriptionBuilder _descriptionBuilder;
    private readonly CapabilityParameterSchemaBuilder _schemaBuilder;

    public CapabilityToolDescriptorFactory(
        CapabilityToolDescriptionBuilder descriptionBuilder,
        CapabilityParameterSchemaBuilder schemaBuilder)
    {
        _descriptionBuilder = descriptionBuilder ?? throw new ArgumentNullException(nameof(descriptionBuilder));
        _schemaBuilder = schemaBuilder ?? throw new ArgumentNullException(nameof(schemaBuilder));
    }

    public CapabilityToolDescriptor Create(CapabilitySemanticMetadata capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        var schema = _schemaBuilder.Build(capability.Arguments, out var argumentMap);
        return new CapabilityToolDescriptor
        {
            Name = CapabilityFunctionNameMapper.Map(capability.FunctionName, capability.CapabilityKey),
            Capability = capability,
            Description = _descriptionBuilder.Build(capability),
            ParameterSchema = schema,
            Arguments = capability.Arguments,
            ModelToCapabilityArgumentMap = argumentMap
        };
    }
}
