namespace TILSOFTAI.Orchestration.Answering;

public sealed record SummaryPolicy
{
    public const string ModeAi = "ai";
    public const string ModeFallback = "fallback";
    public const string ModeDisabled = "disabled";

    public string Mode { get; init; } = ModeAi;
    public string Style { get; init; } = "business_concise";
    public int MaxSentences { get; init; } = 4;
    public bool IncludeFilters { get; init; } = true;
    public bool IncludeRowCount { get; init; } = true;
    public bool IncludeCaveats { get; init; } = true;
    public IReadOnlyDictionary<string, string> InstructionsByLocale { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> ForbiddenClaims { get; init; } = Array.Empty<string>();

    public void Validate()
    {
        if (!IsSupportedMode(Mode))
        {
            throw new InvalidOperationException("Answer policy summary.mode must be one of ai, fallback, or disabled.");
        }

        if (MaxSentences <= 0)
        {
            throw new InvalidOperationException("Answer policy summary.maxSentences must be greater than zero.");
        }
    }

    public static bool IsSupportedMode(string? mode) =>
        string.Equals(mode, ModeAi, StringComparison.OrdinalIgnoreCase)
        || string.Equals(mode, ModeFallback, StringComparison.OrdinalIgnoreCase)
        || string.Equals(mode, ModeDisabled, StringComparison.OrdinalIgnoreCase);
}
