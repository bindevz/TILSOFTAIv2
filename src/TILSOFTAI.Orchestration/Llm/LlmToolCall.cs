namespace TILSOFTAI.Orchestration.Llm;

public sealed class LlmToolCall
{
    public string Name { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = "{}";
}
