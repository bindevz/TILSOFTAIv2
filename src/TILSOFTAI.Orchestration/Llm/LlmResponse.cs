namespace TILSOFTAI.Orchestration.Llm;

public sealed class LlmResponse
{
    public string? Content { get; set; }
    public List<LlmToolCall> ToolCalls { get; set; } = new();
}
