namespace TILSOFTAI.Orchestration.Llm;

public sealed record LlmStreamEvent(string Type, string? Content = null, LlmToolCall? ToolCall = null, string? Error = null)
{
    public static LlmStreamEvent Delta(string delta) => new("delta", delta);
    public static LlmStreamEvent ToolCallEvent(LlmToolCall call) => new("tool_call", null, call);
    public static LlmStreamEvent Final(string content) => new("final", content);
    public static LlmStreamEvent ErrorEvent(string error) => new("error", null, null, error);
}
