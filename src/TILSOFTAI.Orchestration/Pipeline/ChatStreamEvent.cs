using TILSOFTAI.Orchestration.Llm;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Orchestration.Pipeline;

public sealed record ChatStreamEvent(string Type, object? Payload)
{
    public static ChatStreamEvent Delta(string delta) => new("delta", delta);
    public static ChatStreamEvent ToolCall(LlmToolCall call) => new("tool_call", call);
    public static ChatStreamEvent ToolResult(ToolExecutionRecord record) => new("tool_result", record);
    public static ChatStreamEvent Final(string content) => new("final", content);
    public static ChatStreamEvent Error(string error) => new("error", error);
}
