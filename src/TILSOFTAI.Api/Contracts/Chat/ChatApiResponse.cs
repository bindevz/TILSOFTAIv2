using System.Text.Json.Serialization;
using TILSOFTAI.Domain.Errors;

namespace TILSOFTAI.Api.Contracts.Chat;

public sealed class ChatApiResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public object? Detail { get; set; }

    [JsonPropertyName("blocks")]
    public IReadOnlyList<object>? Blocks { get; set; }

    [JsonPropertyName("answerType")]
    public string? AnswerType { get; set; }

    [JsonPropertyName("provenance")]
    public IReadOnlyDictionary<string, object?>? Provenance { get; set; }

    [JsonPropertyName("conversationId")]
    public string? ConversationId { get; set; } = string.Empty;

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; } = string.Empty;

    [JsonPropertyName("traceId")]
    public string? TraceId { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string? Language { get; set; } = string.Empty;

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("error")]
    public ErrorEnvelope? Error { get; set; }
}
