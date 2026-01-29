using System.Text.Json;
using System.Text.Json.Serialization;

namespace TILSOFTAI.Api.Contracts.Chat;

public sealed class ChatApiRequest
{
    [JsonPropertyName("input")]
    public string Input { get; set; } = string.Empty;

    [JsonPropertyName("allowCache")]
    public bool AllowCache { get; set; } = true;

    [JsonPropertyName("containsSensitive")]
    public bool ContainsSensitive { get; set; }

    [JsonPropertyName("metadata")]
    public JsonElement? Metadata { get; set; }
}
