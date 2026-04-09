using TILSOFTAI.Orchestration.Pipeline;

namespace TILSOFTAI.Supervisor;

public sealed record SupervisorStreamEvent(string Type, object? Payload)
{
    public static SupervisorStreamEvent Delta(string delta) => new("delta", delta);
    public static SupervisorStreamEvent ToolCall(object payload) => new("tool_call", payload);
    public static SupervisorStreamEvent ToolResult(object payload) => new("tool_result", payload);
    public static SupervisorStreamEvent Final(string content) => new("final", content);
    public static SupervisorStreamEvent Error(object? error) => new("error", error);

    public static SupervisorStreamEvent FromChat(ChatStreamEvent evt) => new(evt.Type, evt.Payload);
}
