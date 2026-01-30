using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Sensitivity;
using TILSOFTAI.Orchestration.Pipeline;

namespace TILSOFTAI.Orchestration;

public sealed class OrchestrationEngine : IOrchestrationEngine
{
    private readonly ChatPipeline _chatPipeline;
    private readonly IOptions<StreamingOptions> _streamingOptions;
    private readonly ISensitivityClassifier _sensitivityClassifier;
    private readonly SensitiveDataOptions _sensitiveDataOptions;
    private readonly ILogger<OrchestrationEngine> _logger;

    public OrchestrationEngine(
        ChatPipeline chatPipeline,
        IOptions<StreamingOptions> streamingOptions,
        ISensitivityClassifier sensitivityClassifier,
        IOptions<SensitiveDataOptions> sensitiveDataOptions,
        ILogger<OrchestrationEngine> logger)
    {
        _chatPipeline = chatPipeline ?? throw new ArgumentNullException(nameof(chatPipeline));
        _streamingOptions = streamingOptions ?? throw new ArgumentNullException(nameof(streamingOptions));
        _sensitivityClassifier = sensitivityClassifier ?? throw new ArgumentNullException(nameof(sensitivityClassifier));
        _sensitiveDataOptions = sensitiveDataOptions?.Value ?? throw new ArgumentNullException(nameof(sensitiveDataOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<ChatResult> RunChatAsync(ChatRequest request, TilsoftExecutionContext ctx, CancellationToken ct)
    {
        if (request is null)
        {
            return Task.FromResult(ChatResult.Fail("Input is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Input))
        {
            return Task.FromResult(ChatResult.Fail("Input is required"));
        }

        request.Stream = false;
        request.StreamObserver = null;
        ApplySensitivePolicy(request);

        try
        {
            return _chatPipeline.RunAsync(request, ctx, ct);
        }
        catch (TilsoftApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat orchestration failed.");
            return Task.FromResult(ChatResult.Fail("Chat request failed.", ErrorCode.ChatFailed));
        }
    }

    public async IAsyncEnumerable<ChatStreamEvent> RunChatStreamAsync(
        ChatRequest request,
        TilsoftExecutionContext ctx,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (request is null)
        {
            yield return ChatStreamEvent.Error("Input is required.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(request.Input))
        {
            yield return ChatStreamEvent.Error("Input is required.");
            yield break;
        }

        // Create bounded channel for streaming events
        var capacity = _streamingOptions.Value?.ChannelCapacity ?? 100;
        var channel = Channel.CreateBounded<ChatStreamEvent>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        });

        var terminalEmitted = 0;

        // Create progress observer that writes to channel
        var progress = new Progress<ChatStreamEvent>(evt =>
        {
            if (ct.IsCancellationRequested || Interlocked.CompareExchange(ref terminalEmitted, 0, 0) == 1)
            {
                return;
            }

            var written = channel.Writer.TryWrite(evt);
            if (!written)
            {
                _logger.LogWarning("Failed to write event {EventType} to channel.", evt.Type);
            }

            if (IsTerminal(evt.Type))
            {
                Interlocked.Exchange(ref terminalEmitted, 1);
            }
        });

        request.Stream = true;
        request.StreamObserver = progress;
        ApplySensitivePolicy(request);

        // Start pipeline execution in background
        var pipelineTask = Task.Run(async () =>
        {
            ChatResult? result = null;
            try
            {
                result = await _chatPipeline.RunAsync(request, ctx, ct);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is normal, complete the channel
            }
            catch (TilsoftApiException apiEx)
            {
                _logger.LogWarning(apiEx, "Chat pipeline failed during streaming.");
                if (Interlocked.CompareExchange(ref terminalEmitted, 1, 0) == 0)
                {
                    channel.Writer.TryWrite(ChatStreamEvent.Error(new ErrorEnvelope
                    {
                        Code = apiEx.Code,
                        Detail = apiEx.Detail
                    }));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat pipeline failed during streaming.");
                if (Interlocked.CompareExchange(ref terminalEmitted, 1, 0) == 0)
                {
                    channel.Writer.TryWrite(ChatStreamEvent.Error("Chat request failed."));
                }
            }
            finally
            {
                // Ensure terminal event if pipeline completed without emitting one
                if (Interlocked.CompareExchange(ref terminalEmitted, 1, 0) == 0 && result is not null)
                {
                    if (result.Success)
                    {
                        channel.Writer.TryWrite(ChatStreamEvent.Final(result.Content ?? string.Empty));
                    }
                    else
                    {
                        channel.Writer.TryWrite(ChatStreamEvent.Error(result.Error ?? "Chat request failed."));
                    }
                }

                channel.Writer.Complete();
            }
        }, ct);

        // Yield events from channel as they arrive
        await foreach (var evt in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return evt;

            if (IsTerminal(evt.Type))
            {
                break;
            }
        }

        // Ensure pipeline task completes
        try
        {
            await pipelineTask;
        }
        catch (OperationCanceledException)
        {
            // Expected during cancellation
        }
    }

    private static bool IsTerminal(string? eventType)
    {
        return string.Equals(eventType, "final", StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, "error", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplySensitivePolicy(ChatRequest request)
    {
        var input = request.Input ?? string.Empty;
        var sensitivity = _sensitivityClassifier.Classify(input);

        request.ContainsSensitive = sensitivity.ContainsSensitive;
        request.SensitivityReasons = sensitivity.Reasons;
        request.RequestPolicy = new RequestPolicy
        {
            ContainsSensitive = sensitivity.ContainsSensitive,
            HandlingMode = _sensitiveDataOptions.HandlingMode,
            DisableCachingWhenSensitive = _sensitiveDataOptions.DisableCachingWhenSensitive,
            DisableToolResultPersistenceWhenSensitive = _sensitiveDataOptions.DisableToolResultPersistenceWhenSensitive
        };
    }
}
