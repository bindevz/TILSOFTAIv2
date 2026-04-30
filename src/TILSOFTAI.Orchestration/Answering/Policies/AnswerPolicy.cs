using System.Text.Json;

namespace TILSOFTAI.Orchestration.Answering;

public sealed record AnswerPolicy
{
    public int MaxRowsForChat { get; init; } = 20;
    public int MaxRowsForNarration { get; init; } = 20;
    public SummaryPolicy Summary { get; init; } = new();
    public TablePolicy Table { get; init; } = new();
    public NoDataPolicy NoData { get; init; } = new();
    public FollowUpPolicy FollowUp { get; init; } = new();
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
            EnsureRequiredCatalogFields(rawJson);
            var parsed = JsonSerializer.Deserialize<AnswerPolicy>(rawJson, JsonOptions.Default)
                ?? throw new InvalidOperationException("Answer policy JSON deserialized to null.");
            return parsed.Validate() with { RawJson = rawJson };
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Answer policy JSON is invalid.", ex);
        }
    }

    public AnswerPolicy Validate()
    {
        if (MaxRowsForChat <= 0)
        {
            throw new InvalidOperationException("Answer policy maxRowsForChat must be greater than zero.");
        }

        if (MaxRowsForNarration <= 0)
        {
            throw new InvalidOperationException("Answer policy maxRowsForNarration must be greater than zero.");
        }

        Summary.Validate();
        Table.Validate();

        return this;
    }

    private static void EnsureRequiredCatalogFields(string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        var root = document.RootElement;
        RequireProperty(root, "maxRowsForChat");
        RequireProperty(root, "maxRowsForNarration");
        RequireProperty(root, "summary");
        RequireProperty(root, "table");
        RequireProperty(root, "noData");
        RequireProperty(root, "followUp");
    }

    private static void RequireProperty(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out _))
        {
            throw new InvalidOperationException($"Answer policy JSON is missing required property '{propertyName}'.");
        }
    }
}
