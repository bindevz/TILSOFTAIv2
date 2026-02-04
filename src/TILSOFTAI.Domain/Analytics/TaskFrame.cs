using System.Text.Json.Serialization;

namespace TILSOFTAI.Domain.Analytics;

/// <summary>
/// Represents the structured task frame derived from user intent.
/// </summary>
public sealed class TaskFrame
{
    [JsonPropertyName("taskType")]
    public TaskType TaskType { get; set; } = TaskType.Analytics;

    [JsonPropertyName("entity")]
    public string? Entity { get; set; }

    [JsonPropertyName("metrics")]
    public List<MetricSpec> Metrics { get; set; } = new();

    [JsonPropertyName("filters")]
    public List<FilterSpec> Filters { get; set; } = new();

    [JsonPropertyName("breakdowns")]
    public List<string> Breakdowns { get; set; } = new();

    [JsonPropertyName("timeRangeHint")]
    public string? TimeRangeHint { get; set; }

    [JsonPropertyName("needsVisualization")]
    public bool NeedsVisualization { get; set; }

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskType
{
    Analytics,
    Lookup,
    Explain,
    Mixed
}

public sealed class MetricSpec
{
    [JsonPropertyName("op")]
    public string Op { get; set; } = "count";

    [JsonPropertyName("fieldHint")]
    public string? FieldHint { get; set; }

    [JsonPropertyName("as")]
    public string? As { get; set; }
}

public sealed class FilterSpec
{
    [JsonPropertyName("fieldHint")]
    public string? FieldHint { get; set; }

    [JsonPropertyName("op")]
    public string Op { get; set; } = "eq";

    [JsonPropertyName("value")]
    public object? Value { get; set; }
}
