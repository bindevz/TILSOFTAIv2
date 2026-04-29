namespace TILSOFTAI.Orchestration.Answering;

public abstract record AnswerBlock(string Type);

public sealed record TextBlock(string Content) : AnswerBlock("text");

public sealed record SummaryBlock(string Content) : AnswerBlock("summary");

public sealed record TableBlock(
    string Title,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    int RowCount,
    int DisplayedRows,
    bool Truncated) : AnswerBlock("table")
{
    public int TotalRows => RowCount;
}

public sealed record ChartBlock(
    string ChartType,
    string CategoryField,
    string ValueField,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Data) : AnswerBlock("chart");

public sealed record FollowUpBlock(
    string Question,
    IReadOnlyList<string> Options) : AnswerBlock("follow_up");

public sealed record ConfirmationBlock(
    string Title,
    string Summary,
    IReadOnlyDictionary<string, object?> DraftAction) : AnswerBlock("confirmation");

public sealed record RawJsonBlock(object Data) : AnswerBlock("raw_json");
