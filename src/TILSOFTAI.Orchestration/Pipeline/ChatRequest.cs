using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Conversations;

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

    /// <summary>
    /// Full conversation history (user + assistant turns).
    /// When provided, ChatPipeline injects these into the LLM messages list.
    /// The last message should be the current user query.
    /// </summary>
    public IReadOnlyList<ChatMessage>? MessageHistory { get; set; }
}
