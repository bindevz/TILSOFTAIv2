namespace TILSOFTAI.Domain.Errors;

public sealed class ErrorEnvelope
{
    public string Code { get; set; } = string.Empty;
    public string MessageKey { get; set; } = string.Empty;
    public string LocalizedMessage { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public object? Detail { get; set; }
    
    // Trace fields for observability
    public string? CorrelationId { get; set; }
    public string? TraceId { get; set; }
    public string? RequestId { get; set; }
}
