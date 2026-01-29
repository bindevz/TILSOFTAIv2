namespace TILSOFTAI.Orchestration.Llm;

public sealed class NullLlmClient : ILlmClient
{
    public Task<LlmResponse> CompleteAsync(LlmRequest req, CancellationToken ct)
    {
        return Task.FromResult(new LlmResponse
        {
            Content = "LLM client not configured."
        });
    }

    public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest req, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        yield return LlmStreamEvent.Delta("LLM client not configured.");
        yield return LlmStreamEvent.Final("LLM client not configured.");
        await Task.CompletedTask;
    }
}
