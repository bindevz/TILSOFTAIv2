using System.Text;
using System.Threading.Channels;
using FluentAssertions;
using Xunit;

namespace TILSOFTAI.Tests.Streaming;

/// <summary>
/// PATCH 33.01: Tests for streaming delta coalescing.
/// Verifies that no content is lost and ordering is preserved.
/// </summary>
public class ChatStreamCoalescingTests
{
    /// <summary>
    /// Simulates the delta coalescing logic from OrchestrationEngine.
    /// Given many small delta chars, the coalescer should:
    /// (1) Not drop any characters
    /// (2) Preserve total length
    /// </summary>
    [Fact]
    public async Task DeltaCoalescing_1000Deltas_ShouldPreserveAllContent()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = true
        });

        var deltaLock = new object();
        var deltaBuffer = new StringBuilder();
        var maxDeltaChars = 50; // small buffer to force multiple flushes

        int flushCount = 0;

        void FlushDeltaUnsafe()
        {
            if (deltaBuffer.Length == 0) return;
            var text = deltaBuffer.ToString();
            deltaBuffer.Clear();
            channel.Writer.TryWrite(text);
            Interlocked.Increment(ref flushCount);
        }

        // Act: simulate 1000 single-char deltas
        const int totalDeltas = 1000;
        for (int i = 0; i < totalDeltas; i++)
        {
            lock (deltaLock)
            {
                deltaBuffer.Append('a');
                if (deltaBuffer.Length >= maxDeltaChars) FlushDeltaUnsafe();
            }
        }

        // Final flush
        lock (deltaLock) FlushDeltaUnsafe();
        channel.Writer.Complete();

        // Read all coalesced chunks
        var result = new StringBuilder();
        await foreach (var chunk in channel.Reader.ReadAllAsync())
        {
            result.Append(chunk);
        }

        // Assert
        result.Length.Should().Be(totalDeltas, "no characters should be lost during coalescing");
        result.ToString().Should().Be(new string('a', totalDeltas));
        flushCount.Should().BeGreaterThan(1, "coalescing should have produced multiple flushes");
    }

    /// <summary>
    /// Verifies that non-delta events cause immediate flush of buffered deltas,
    /// preserving ordering relative to tool_result and final events.
    /// </summary>
    [Fact]
    public async Task DeltaCoalescing_WithInterleavedEvents_ShouldPreserveOrder()
    {
        // Arrange
        var events = new List<(string type, string content)>();
        var channel = Channel.CreateUnbounded<(string type, string content)>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        var deltaLock = new object();
        var deltaBuffer = new StringBuilder();

        void FlushDeltaUnsafe()
        {
            if (deltaBuffer.Length == 0) return;
            var text = deltaBuffer.ToString();
            deltaBuffer.Clear();
            channel.Writer.TryWrite(("delta", text));
        }

        void WriteEvent(string type, string content)
        {
            if (type == "delta")
            {
                lock (deltaLock)
                {
                    deltaBuffer.Append(content);
                }
            }
            else
            {
                // Flush delta before non-delta
                lock (deltaLock) FlushDeltaUnsafe();
                channel.Writer.TryWrite((type, content));
            }
        }

        // Act: interleave deltas with tool_result
        WriteEvent("delta", "Hel");
        WriteEvent("delta", "lo ");
        WriteEvent("tool_result", "tool1_result");
        WriteEvent("delta", "Wor");
        WriteEvent("delta", "ld");
        WriteEvent("final", "done");

        // Final flush
        lock (deltaLock) FlushDeltaUnsafe();
        channel.Writer.Complete();

        // Read
        var collected = new List<(string type, string content)>();
        await foreach (var e in channel.Reader.ReadAllAsync())
        {
            collected.Add(e);
        }

        // Assert ordering
        collected.Should().HaveCountGreaterOrEqualTo(4);

        // Delta content before tool_result must be "Hello "
        var firstDeltaIdx = collected.FindIndex(e => e.type == "delta");
        var toolIdx = collected.FindIndex(e => e.type == "tool_result");
        firstDeltaIdx.Should().BeLessThan(toolIdx, "delta should appear before tool_result");
        collected[firstDeltaIdx].content.Should().Be("Hel" + "lo ", "delta before tool must be flushed");

        // After tool_result, we should have another delta ("World") then final
        var secondDeltaIdx = collected.FindIndex(toolIdx, e => e.type == "delta");
        secondDeltaIdx.Should().BeGreaterThan(toolIdx);
        collected[secondDeltaIdx].content.Should().Be("Wor" + "ld");

        var finalIdx = collected.FindIndex(e => e.type == "final");
        finalIdx.Should().BeGreaterThan(secondDeltaIdx, "final must be after all deltas");

        // Total delta content
        var totalDeltaContent = string.Concat(collected.Where(e => e.type == "delta").Select(e => e.content));
        totalDeltaContent.Should().Be("Hello World");
    }
}
