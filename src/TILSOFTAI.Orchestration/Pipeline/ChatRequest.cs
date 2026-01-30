using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Pipeline;

public sealed class ChatRequest
{
    public string Input { get; set; } = string.Empty;
    public bool Stream { get; set; }
    public IProgress<ChatStreamEvent>? StreamObserver { get; set; }
    public bool AllowCache { get; set; } = true;
    
    /// <summary>
    /// Server-computed sensitivity flag. Not from client input.
    /// </summary>
    public bool ContainsSensitive { get; set; }
    
    /// <summary>
    /// Optional reasons for sensitivity classification.
    /// Used for observability and tuning.
    /// </summary>
    public IReadOnlyList<string>? SensitivityReasons { get; set; }

    /// <summary>
    /// Server-computed request policy for sensitive handling.
    /// </summary>
    public RequestPolicy? RequestPolicy { get; set; }
}
