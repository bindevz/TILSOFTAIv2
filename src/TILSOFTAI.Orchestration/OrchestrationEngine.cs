using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Domain.Sensitivity;
using TILSOFTAI.Orchestration.Pipeline;

namespace TILSOFTAI.Orchestration;

public sealed class OrchestrationEngine : IOrchestrationEngine
{
    private readonly ChatPipeline _chatPipeline;
    private readonly IOptions<StreamingOptions> _streamingOptions;
    private readonly ISensitivityClassifier _sensitivityClassifier;
    private readonly SensitiveDataOptions _sensitiveDataOptions;
    private readonly IMetricsService _metrics;
    private readonly ISqlErrorLogWriter _errorLogWriter;
    private readonly ObservabilityOptions _observabilityOptions;
    private readonly ILogger<OrchestrationEngine> _logger;

    public OrchestrationEngine(
        ChatPipeline chatPipeline,
        IOptions<StreamingOptions> streamingOptions,
        ISensitivityClassifier sensitivityClassifier,
        IOptions<SensitiveDataOptions> sensitiveDataOptions,
        IMetricsService metrics,
        ISqlErrorLogWriter errorLogWriter,
        IOptions<ObservabilityOptions> observabilityOptions,
        ILogger<OrchestrationEngine> logger)
    {
        _chatPipeline = chatPipeline ?? throw new ArgumentNullException(nameof(chatPipeline));
        _streamingOptions = streamingOptions ?? throw new ArgumentNullException(nameof(streamingOptions));
        _sensitivityClassifier = sensitivityClassifier ?? throw new ArgumentNullException(nameof(sensitivityClassifier));
        _sensitiveDataOptions = sensitiveDataOptions?.Value ?? throw new ArgumentNullException(nameof(sensitiveDataOptions));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _errorLogWriter = errorLogWriter ?? throw new ArgumentNullException(nameof(errorLogWriter));
        _observabilityOptions = observabilityOptions?.Value ?? throw new ArgumentNullException(nameof(observabilityOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ChatResult> RunChatAsync(ChatRequest request, TilsoftExecutionContext ctx, CancellationToken ct)
    {
        if (request is null)
        {
            return ChatResult.Fail("Input is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Input))
        {
            return ChatResult.Fail("Input is required");
        }

        request.Stream = false;
        request.StreamObserver = null;
        ApplySensitivePolicy(request);

        ChatResult result;
        try
        {
            result = await _chatPipeline.RunAsync(request, ctx, ct);
        }
        catch (TilsoftApiException)
        {
            throw;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat orchestration failed.");
            await TryWriteErrorLogAsync(ctx, "CHAT_FAILED", "Chat request failed.", ex, ct);
            return ChatResult.Fail("Chat request failed.", ErrorCode.ChatFailed);
        }

        // PATCH 33.04: Persist non-exception failures to ErrorLog
        if (!result.Success && !ct.IsCancellationRequested)
        {
            await TryWriteErrorLogAsync(ctx, result.Code ?? "CHAT_FAILED", result.Error ?? "Chat request failed.", null, ct);
        }

        return result;
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

        // PATCH 33.01: Unbounded channel (never drops)
        var channel = Channel.CreateUnbounded<ChatStreamEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = true
        });

        var terminalEmitted = 0;

        // PATCH 33.01: Delta coalescing
        var flushIntervalMs = Math.Clamp(_streamingOptions.Value?.DeltaFlushIntervalMs ?? 40, 10, 200);
        var maxDeltaChars = Math.Clamp(_streamingOptions.Value?.MaxDeltaBufferChars ?? 512, 64, 4096);

        var deltaLock = new object();
        var deltaBuffer = new StringBuilder();

        void FlushDeltaUnsafe()
        {
            if (deltaBuffer.Length == 0) return;
            var text = deltaBuffer.ToString();
            deltaBuffer.Clear();
            channel.Writer.TryWrite(ChatStreamEvent.Delta(text));
            _metrics.IncrementCounter(MetricNames.ChatStreamDeltasOutTotal);
            _metrics.IncrementCounter(MetricNames.ChatStreamDeltaFlushTotal);
        }

        // Create progress observer that writes to channel with delta coalescing
        // PATCH 33 FIX: Use synchronous IProgress to preserve delta ordering
        // (Progress<T> posts to thread pool → out-of-order callbacks → garbled text)
        IProgress<ChatStreamEvent> progress = new SynchronousProgress<ChatStreamEvent>(evt =>
        {
            if (ct.IsCancellationRequested || Interlocked.CompareExchange(ref terminalEmitted, 0, 0) == 1)
            {
                return;
            }

            if (evt.Type == "delta" && evt.Payload is string s && !string.IsNullOrEmpty(s))
            {
                _metrics.IncrementCounter(MetricNames.ChatStreamDeltasInTotal);
                lock (deltaLock)
                {
                    deltaBuffer.Append(s);
                    if (deltaBuffer.Length >= maxDeltaChars) FlushDeltaUnsafe();
                }
                return;
            }

            // Flush buffered delta before any non-delta event to preserve ordering
            lock (deltaLock) FlushDeltaUnsafe();
            channel.Writer.TryWrite(evt);

            if (IsTerminal(evt.Type))
            {
                Interlocked.Exchange(ref terminalEmitted, 1);
            }
        });

        request.Stream = true;
        request.StreamObserver = progress;
        ApplySensitivePolicy(request);

        // Background periodic flush task
        var flusher = Task.Run(async () =>
        {
            try
            {
                while (!ct.IsCancellationRequested && Interlocked.CompareExchange(ref terminalEmitted, 0, 0) == 0)
                {
                    await Task.Delay(flushIntervalMs, ct);
                    lock (deltaLock) FlushDeltaUnsafe();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation
            }
        }, ct);

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
                await TryWriteErrorLogAsync(ctx, apiEx.Code ?? "CHAT_FAILED", "Chat pipeline failed during streaming.", apiEx, ct);
                if (Interlocked.CompareExchange(ref terminalEmitted, 1, 0) == 0)
                {
                    lock (deltaLock) FlushDeltaUnsafe();
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
                await TryWriteErrorLogAsync(ctx, "CHAT_FAILED", "Chat pipeline failed during streaming.", ex, ct);
                if (Interlocked.CompareExchange(ref terminalEmitted, 1, 0) == 0)
                {
                    lock (deltaLock) FlushDeltaUnsafe();
                    channel.Writer.TryWrite(ChatStreamEvent.Error("Chat request failed."));
                }
            }
            finally
            {
                // Ensure terminal event if pipeline completed without emitting one
                if (Interlocked.CompareExchange(ref terminalEmitted, 1, 0) == 0 && result is not null)
                {
                    lock (deltaLock) FlushDeltaUnsafe();

                    if (result.Success)
                    {
                        channel.Writer.TryWrite(ChatStreamEvent.Final(result.Content ?? string.Empty));
                    }
                    else
                    {
                        // PATCH 33.04: Persist pipeline Fail to ErrorLog
                        if (!ct.IsCancellationRequested)
                        {
                            await TryWriteErrorLogAsync(ctx, result.Code ?? "CHAT_FAILED", result.Error ?? "Chat request failed.", null, ct);
                        }
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
            await flusher;
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

    // PATCH 33.04: Best-effort ErrorLog persistence
    private async Task TryWriteErrorLogAsync(TilsoftExecutionContext ctx, string code, string message, Exception? ex, CancellationToken ct)
    {
        if (!_observabilityOptions.EnableSqlErrorLog) return;

        try
        {
            object? detail = ex is not null
                ? new { ExceptionType = ex.GetType().Name, ex.Message }
                : null;
            await _errorLogWriter.WriteAsync(ctx, code, message, detail, ct);
        }
        catch (Exception logEx)
        {
            _logger.LogWarning(logEx, "Failed to persist error to ErrorLog.");
        }
    }
}

/// <summary>
/// PATCH 33 FIX: Synchronous IProgress implementation.
/// Unlike Progress&lt;T&gt; which posts to SynchronizationContext/ThreadPool,
/// this runs the handler inline on the caller's thread, preserving call order.
/// </summary>
internal sealed class SynchronousProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;

    public SynchronousProgress(Action<T> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public void Report(T value) => _handler(value);
}
