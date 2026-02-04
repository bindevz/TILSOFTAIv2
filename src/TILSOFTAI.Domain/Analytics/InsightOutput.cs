using System.Text.Json.Serialization;

namespace TILSOFTAI.Domain.Analytics;

/// <summary>
/// Represents the structured insight output with stable format.
/// </summary>
public sealed class InsightOutput
{
    [JsonPropertyName("headline")]
    public InsightHeadline Headline { get; set; } = new();

    [JsonPropertyName("tables")]
    public List<InsightTable> Tables { get; set; } = new();

    [JsonPropertyName("notes")]
    public List<string> Notes { get; set; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    [JsonPropertyName("freshness")]
    public DataFreshness? Freshness { get; set; }
}

public sealed class InsightHeadline
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";
}

public sealed class InsightTable
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("columns")]
    public List<string> Columns { get; set; } = new();

    [JsonPropertyName("rows")]
    public List<List<object?>> Rows { get; set; } = new();

    [JsonPropertyName("topN")]
    public int? TopN { get; set; }
}

public sealed class DataFreshness
{
    [JsonPropertyName("asOfUtc")]
    public DateTime AsOfUtc { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = "SQL";
}
