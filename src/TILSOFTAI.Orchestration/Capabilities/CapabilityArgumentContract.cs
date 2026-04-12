namespace TILSOFTAI.Orchestration.Capabilities;

public sealed class CapabilityArgumentContract
{
    public string ContractVersion { get; init; } = "1";
    public string SchemaDialect { get; init; } = string.Empty;
    public string SchemaRef { get; init; } = string.Empty;
    public IReadOnlyList<string> RequiredArguments { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AllowedArguments { get; init; } = Array.Empty<string>();
    public bool AllowAdditionalArguments { get; init; } = true;
    public IReadOnlyList<CapabilityArgumentRule> Arguments { get; init; } = Array.Empty<CapabilityArgumentRule>();
}

public sealed class CapabilityArgumentRule
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Format { get; init; } = string.Empty;
    public IReadOnlyList<string> Enum { get; init; } = Array.Empty<string>();
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public string Pattern { get; init; } = string.Empty;
}
