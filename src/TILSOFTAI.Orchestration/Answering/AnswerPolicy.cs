using System.Text.Json;

namespace TILSOFTAI.Orchestration.Answering;

public sealed record AnswerPolicy
{
    public int MaxRowsForChat { get; init; } = 20;
    public int MaxRowsForAiSummary { get; init; } = 20;
    public bool AllowAiSummary { get; init; }
    public string? RawJson { get; init; }

    public static AnswerPolicy Default { get; } = new();

    public static AnswerPolicy FromJson(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return Default;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<AnswerPolicy>(rawJson, JsonOptions.Default);
            return (parsed ?? Default) with { RawJson = rawJson };
        }
        catch (JsonException)
        {
            return Default with { RawJson = rawJson };
        }
    }
}
