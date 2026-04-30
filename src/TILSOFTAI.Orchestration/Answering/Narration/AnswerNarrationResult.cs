namespace TILSOFTAI.Orchestration.Answering.Narration;

public sealed record AnswerNarrationResult
{
    public required string Text { get; init; }
    public double? Confidence { get; init; }
    public IReadOnlyList<string> UsedColumns { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public bool UsedFallback { get; init; }
}
