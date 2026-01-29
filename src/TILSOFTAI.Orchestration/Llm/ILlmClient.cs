namespace TILSOFTAI.Orchestration.Llm;

public interface ILlmClient
{
    Task<LlmResponse> CompleteAsync(LlmRequest req, CancellationToken ct);
    IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest req, CancellationToken ct);
}
