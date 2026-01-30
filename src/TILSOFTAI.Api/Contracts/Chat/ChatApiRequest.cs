using System.Text.Json;
using System.Text.Json.Serialization;

namespace TILSOFTAI.Api.Contracts.Chat;

public sealed class ChatApiRequest
{
    [JsonPropertyName("input")]
    public string Input { get; set; } = string.Empty;

    [JsonPropertyName("allowCache")]
    public bool AllowCache { get; set; } = true;

    /// <summary>
    /// DEPRECATED: This flag is ignored. Sensitivity is now computed server-side.
    /// Kept for backward compatibility only.
    /// </summary>
    [JsonPropertyName("containsSensitive")]
    public bool ContainsSensitive { get; set; }

    [JsonPropertyName("metadata")]
    public JsonElement? Metadata { get; set; }
}
