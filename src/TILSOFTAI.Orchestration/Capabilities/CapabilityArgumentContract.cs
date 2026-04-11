namespace TILSOFTAI.Orchestration.Capabilities;

public sealed class CapabilityArgumentContract
{
    public IReadOnlyList<string> RequiredArguments { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AllowedArguments { get; init; } = Array.Empty<string>();
    public bool AllowAdditionalArguments { get; init; } = true;
}
