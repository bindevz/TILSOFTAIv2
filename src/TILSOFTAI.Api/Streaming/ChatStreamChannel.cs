using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Contracts.Chat;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Api.Streaming;

public sealed class ChatStreamChannel
{
    private readonly Channel<ChatStreamEventEnvelope> _channel;
    private readonly bool _dropDeltaWhenFull;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ChatStreamChannel(IOptions<StreamingOptions> options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var settings = options.Value ?? new StreamingOptions();
        var capacity = settings.ChannelCapacity > 0 ? settings.ChannelCapacity : 256;

        _dropDeltaWhenFull = settings.DropDeltaWhenFull;
        _channel = Channel.CreateBounded<ChatStreamEventEnvelope>(new BoundedChannelOptions(capacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public bool TryWrite(ChatStreamEventEnvelope envelope)
    {
        if (envelope is null)
        {
            throw new ArgumentNullException(nameof(envelope));
        }

        _gate.Wait();
        try
        {
            if (_channel.Writer.TryWrite(envelope))
            {
                return true;
            }

            if (!_dropDeltaWhenFull)
            {
                return false;
            }

            return TryDropOldestDelta(envelope);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Complete(Exception? error = null)
    {
        _channel.Writer.TryComplete(error);
    }

    public async IAsyncEnumerable<ChatStreamEventEnvelope> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            List<ChatStreamEventEnvelope> batch;
            await _gate.WaitAsync(cancellationToken);
            try
            {
                batch = new List<ChatStreamEventEnvelope>();
                while (_channel.Reader.TryRead(out var item))
                {
                    batch.Add(item);
                }
            }
            finally
            {
                _gate.Release();
            }

            foreach (var item in batch)
            {
                yield return item;
            }
        }
    }

    private bool TryDropOldestDelta(ChatStreamEventEnvelope envelope)
    {
        var buffered = new List<ChatStreamEventEnvelope>();
        var dropped = false;

        while (_channel.Reader.TryRead(out var item))
        {
            if (!dropped && IsDelta(item))
            {
                dropped = true;
                continue;
            }

            buffered.Add(item);
        }

        foreach (var item in buffered)
        {
            _channel.Writer.TryWrite(item);
        }

        return dropped && _channel.Writer.TryWrite(envelope);
    }

    private static bool IsDelta(ChatStreamEventEnvelope envelope)
    {
        return string.Equals(envelope.Type, "delta", StringComparison.OrdinalIgnoreCase);
    }
}
