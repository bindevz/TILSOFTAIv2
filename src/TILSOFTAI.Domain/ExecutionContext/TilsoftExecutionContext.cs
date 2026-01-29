namespace TILSOFTAI.Domain.ExecutionContext;

public sealed class TilsoftExecutionContext
{
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
    public string CorrelationId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
}
