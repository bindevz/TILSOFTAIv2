using TILSOFTAI.Infrastructure.Llm;
using TILSOFTAI.Orchestration.Llm;
using Xunit;

namespace TILSOFTAI.Tests.Integration;

public sealed class OpenAiParsingTests
{
    [Fact]
    public void ParseCompletion_WithToolCalls()
    {
        const string json = """
{
  "choices": [
    {
      "message": {
        "content": "hello",
        "tool_calls": [
          {
            "id": "call_1",
            "type": "function",
            "function": {
              "name": "tool.list",
              "arguments": "{\"limit\":10}"
            }
          }
        ]
      }
    }
  ]
}
""";

        var response = OpenAiResponseParser.ParseCompletion(json);

        Assert.Equal("hello", response.Content);
        Assert.Single(response.ToolCalls);
        Assert.Equal("tool.list", response.ToolCalls[0].Name);
        Assert.Equal("{\"limit\":10}", response.ToolCalls[0].ArgumentsJson);
    }

    [Fact]
    public void ParseStream_EmitsDeltaToolCallAndFinal()
    {
        var state = new OpenAiStreamState();
        var events = new List<LlmStreamEvent>();

        var chunks = new[]
        {
            """{"choices":[{"delta":{"content":"hello "}}]}""",
            """{"choices":[{"delta":{"content":"world"}}]}""",
            """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_1","type":"function","function":{"name":"tool.list","arguments":"{\"limit\":10"}}]}}]}""",
            """{"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"}"}}]},"finish_reason":"tool_calls"}]}""",
        };

        foreach (var chunk in chunks)
        {
            events.AddRange(OpenAiResponseParser.HandleStreamChunk(state, chunk));
        }

        events.AddRange(OpenAiResponseParser.FinalizeStream(state));

        Assert.Contains(events, evt => evt.Type == "delta");
        Assert.Contains(events, evt => evt.Type == "tool_call");
        var finalEvent = Assert.Single(events, evt => evt.Type == "final");
        Assert.Equal("hello world", finalEvent.Content);

        var toolCallEvent = Assert.Single(events, evt => evt.Type == "tool_call");
        Assert.Equal("tool.list", toolCallEvent.ToolCall?.Name);
        Assert.Equal("{\"limit\":10}", toolCallEvent.ToolCall?.ArgumentsJson);
    }
}
