namespace TILSOFTAI.Orchestration.Answering;

public sealed record AssistantAnswer
{
    public required string AnswerType { get; init; }
    public required string Text { get; init; }
    public required IReadOnlyList<AnswerBlock> Blocks { get; init; }
    public object? Detail { get; init; }
    public IReadOnlyList<string> FollowUpQuestions { get; init; } = Array.Empty<string>();
    public required AnswerProvenance Provenance { get; init; }
    public string? CorrelationId { get; init; }
    public string Locale { get; init; } = "en-US";
    public string Content => Text;
    public string? SelectedAgentId { get; init; }
}

public sealed record AnswerProvenance
{
    public required string CapabilityKey { get; init; }
    public int RowCount { get; init; }
    public string? CorrelationId { get; init; }
    public string? ProcedureName { get; init; }
}
