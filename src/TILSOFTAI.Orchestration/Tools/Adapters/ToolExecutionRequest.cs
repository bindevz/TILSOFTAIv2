namespace TILSOFTAI.Tools.Abstractions;

public sealed class ToolExecutionRequest
{
    public string TenantId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string SystemId { get; set; } = string.Empty;
    public string CapabilityKey { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = "{}";
    public string ExecutionMode { get; set; } = "sync";
    public string CorrelationId { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string?> Metadata { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}
