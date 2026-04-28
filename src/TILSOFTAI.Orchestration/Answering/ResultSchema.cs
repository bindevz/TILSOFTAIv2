using System.Text.Json;

namespace TILSOFTAI.Orchestration.Answering;

public sealed record ResultSchema
{
    public IReadOnlyList<ResultColumn> Columns { get; init; } = Array.Empty<ResultColumn>();
    public IReadOnlyList<ResultSort> DefaultSort { get; init; } = Array.Empty<ResultSort>();
    public IReadOnlyList<ResultChartHint> ChartHints { get; init; } = Array.Empty<ResultChartHint>();
    public string? RawJson { get; init; }

    public static ResultSchema? FromJson(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<ResultSchema>(rawJson, JsonOptions.Default);
            return parsed is null ? null : parsed with { RawJson = rawJson };
        }
        catch (JsonException)
        {
            return new ResultSchema { RawJson = rawJson };
        }
    }
}

public sealed record ResultColumn
{
    public required string Name { get; init; }
    public string? Label { get; init; }
    public string? Type { get; init; }
    public string? Role { get; init; }
    public string? Format { get; init; }
    public bool Visible { get; init; } = true;
}

public sealed record ResultSort
{
    public required string Column { get; init; }
    public string Direction { get; init; } = "asc";
}

public sealed record ResultChartHint
{
    public required string Type { get; init; }
    public required string Category { get; init; }
    public required string Value { get; init; }
}

internal static class JsonOptions
{
    public static JsonSerializerOptions Default { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
}
