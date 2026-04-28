namespace TILSOFTAI.Orchestration.Answering;

public abstract record AnswerBlock(string Type);

public sealed record TextBlock(string Content) : AnswerBlock("text");

public sealed record TableBlock(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    int TotalRows,
    bool Truncated) : AnswerBlock("table");

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
