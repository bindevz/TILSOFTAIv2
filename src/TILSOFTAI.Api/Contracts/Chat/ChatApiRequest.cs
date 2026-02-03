using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TILSOFTAI.Api.Contracts.Chat;

public sealed class ChatApiRequest
{
    [JsonPropertyName("input")]
    [Required(ErrorMessage = "Input is required.")]
    [MaxLength(32000, ErrorMessage = "Input cannot exceed 32000 characters.")]
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

    /// <summary>
    /// Optional preferred language for this request (e.g., "en", "es", "fr").
    /// If not provided or invalid, uses the connection-level language or default.
    /// </summary>
    [JsonPropertyName("preferredLanguage")]
    public string? PreferredLanguage { get; set; }
}
