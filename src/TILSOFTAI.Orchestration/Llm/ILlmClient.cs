namespace TILSOFTAI.Orchestration.Llm;

public interface ILlmClient
{
    Task<LlmResponse> CompleteAsync(LlmRequest req, CancellationToken ct);
    IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest req, CancellationToken ct);
    
    /// <summary>
    /// Lightweight check to verify LLM endpoint is reachable.
    /// </summary>
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
}
