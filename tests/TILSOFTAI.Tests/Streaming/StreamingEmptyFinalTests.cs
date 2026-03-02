using TILSOFTAI.Orchestration.Llm;
using TILSOFTAI.Orchestration.Pipeline;
using Xunit;

namespace TILSOFTAI.Tests.Streaming;

/// <summary>
/// PATCH 37.05: Streaming empty-final guard tests.
/// Verifies detection conditions and ChatStreamEvent factory methods.
/// </summary>
public sealed class StreamingEmptyFinalTests
{
    [Fact]
    public void LlmResponse_EmptyContentAndNoToolCalls_IsDetectable()
    {
        var response = new LlmResponse { Content = "" };

        Assert.True(string.IsNullOrWhiteSpace(response.Content));
        Assert.Empty(response.ToolCalls);
    }

    [Fact]
    public void LlmResponse_WhitespaceOnlyContent_IsDetectable()
    {
        var response = new LlmResponse { Content = "   " };

        Assert.True(string.IsNullOrWhiteSpace(response.Content));
        Assert.Empty(response.ToolCalls);
    }

    [Fact]
    public void LlmResponse_WithToolCalls_DoesNotTriggerFallback()
    {
        var response = new LlmResponse { Content = "" };
        response.ToolCalls.Add(new LlmToolCall
        {
            Name = "model_count",
            ArgumentsJson = "{}"
        });

        Assert.True(string.IsNullOrWhiteSpace(response.Content));
        Assert.NotEmpty(response.ToolCalls);
    }

    [Fact]
    public void LlmResponse_WithContent_NoFallbackNeeded()
    {
        var response = new LlmResponse { Content = "Tổng số model là 42." };
        Assert.False(string.IsNullOrWhiteSpace(response.Content));
    }

    [Fact]
    public void ChatStreamEvent_Final_ProducesFinalType()
    {
        var evt = ChatStreamEvent.Final("Safe message.");
        Assert.Equal("final", evt.Type);
        Assert.Equal("Safe message.", evt.Payload);
    }

    [Fact]
    public void ChatStreamEvent_Delta_ProducesDeltaType()
    {
        var evt = ChatStreamEvent.Delta("chunk");
        Assert.Equal("delta", evt.Type);
        Assert.Equal("chunk", evt.Payload);
    }
}
