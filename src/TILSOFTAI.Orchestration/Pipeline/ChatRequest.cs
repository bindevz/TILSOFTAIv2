namespace TILSOFTAI.Orchestration.Pipeline;

public sealed class ChatRequest
{
    public string Input { get; set; } = string.Empty;
    public bool Stream { get; set; }
    public IProgress<ChatStreamEvent>? StreamObserver { get; set; }
    public bool AllowCache { get; set; } = true;
    public bool ContainsSensitive { get; set; }
}
