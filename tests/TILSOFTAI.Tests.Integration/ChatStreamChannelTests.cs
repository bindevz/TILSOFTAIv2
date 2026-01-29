using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Contracts.Chat;
using TILSOFTAI.Api.Streaming;
using TILSOFTAI.Domain.Configuration;
using Xunit;

namespace TILSOFTAI.Tests.Integration;

public sealed class ChatStreamChannelTests
{
    [Fact]
    public async Task ChatStreamChannel_DropsOldestDeltaWhenFull()
    {
        var options = Options.Create(new StreamingOptions
        {
            ChannelCapacity = 2,
            DropDeltaWhenFull = true
        });

        var channel = new ChatStreamChannel(options);

        channel.TryWrite(new ChatStreamEventEnvelope { Type = "delta", Payload = "a" });
        channel.TryWrite(new ChatStreamEventEnvelope { Type = "delta", Payload = "b" });
        channel.TryWrite(new ChatStreamEventEnvelope { Type = "final", Payload = "done" });
        channel.Complete();

        var events = new List<ChatStreamEventEnvelope>();
        await foreach (var item in channel.ReadAllAsync(CancellationToken.None))
        {
            events.Add(item);
        }

        Assert.DoesNotContain(events, evt => evt.Type == "delta" && (evt.Payload?.ToString() ?? string.Empty) == "a");
        Assert.Contains(events, evt => evt.Type == "delta" && (evt.Payload?.ToString() ?? string.Empty) == "b");
        Assert.Contains(events, evt => evt.Type == "final");
    }
}
