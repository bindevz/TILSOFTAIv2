using System.Text.Json;

namespace TILSOFTAI.Orchestration.Answering;

public sealed record SensitivityPolicy
{
    public IReadOnlyList<string> HiddenColumns { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MaskColumns { get; init; } = Array.Empty<string>();
    public string MaskValue { get; init; } = "***";
    public string? RawJson { get; init; }

    public static SensitivityPolicy Default { get; } = new();

    public static SensitivityPolicy FromJson(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return Default;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<SensitivityPolicy>(rawJson, JsonOptions.Default);
            return (parsed ?? Default) with { RawJson = rawJson };
        }
        catch (JsonException)
        {
            return Default with { RawJson = rawJson };
        }
    }
}
