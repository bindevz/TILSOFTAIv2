using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Orchestration.Llm;

public sealed class LlmRequest
{
    public string SystemPrompt { get; set; } = string.Empty;
    public List<LlmMessage> Messages { get; set; } = new();
    public IReadOnlyList<ToolDefinition> Tools { get; set; } = Array.Empty<ToolDefinition>();
    public int MaxTokens { get; set; } = 4096;
    public bool Stream { get; set; }
}
