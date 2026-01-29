namespace TILSOFTAI.Domain.Errors;

public sealed class ErrorEnvelope
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public object? Detail { get; set; }
}
