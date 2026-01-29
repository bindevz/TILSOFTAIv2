using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Contracts.Chat;
using TILSOFTAI.Api.Streaming;
using TILSOFTAI.Domain.Configuration;
using Xunit;

namespace TILSOFTAI.Tests.Integration;

public sealed class OpenAiStreamChannelTests
{
    [Fact]
    public async Task OpenAiStreamChannel_HandlesBurstWithoutBlocking()
    {
        var options = Options.Create(new StreamingOptions
        {
            ChannelCapacity = 32,
            DropDeltaWhenFull = true
        });

        var channel = new ChatStreamChannel(options);
        var translator = new OpenAiStreamTranslator("chatcmpl_test", 1, "model-x");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var token = cts.Token;

        var chunks = 0;
        var writerTask = Task.Run(async () =>
        {
            await foreach (var envelope in channel.ReadAllAsync(token))
            {
                if (translator.TryTranslate(envelope, out var chunk, out var isTerminal, out _))
                {
                    if (chunk is not null)
                    {
                        chunks++;
                    }
                }

                if (isTerminal)
                {
                    break;
                }
            }
        }, token);

        for (var i = 0; i < 1000; i++)
        {
            channel.TryWrite(new ChatStreamEventEnvelope { Type = "delta", Payload = "x" });
        }

        channel.TryWrite(new ChatStreamEventEnvelope { Type = "final", Payload = "done" });
        channel.Complete();

        await writerTask;
        Assert.True(chunks > 0);
    }
}
