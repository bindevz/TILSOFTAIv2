using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Audit;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Audit;

/// <summary>
/// Background service that consumes audit events and writes to configured sinks.
/// </summary>
public sealed class AuditBackgroundService : BackgroundService
{
    private readonly AuditLogger _auditLogger;
    private readonly IEnumerable<IAuditSink> _sinks;
    private readonly AuditOptions _options;
    private readonly ILogger<AuditBackgroundService> _logger;
    private readonly List<AuditEvent> _batch;

    public AuditBackgroundService(
        AuditLogger auditLogger,
        IEnumerable<IAuditSink> sinks,
        IOptions<AuditOptions> options,
        ILogger<AuditBackgroundService> logger)
    {
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _sinks = sinks ?? throw new ArgumentNullException(nameof(sinks));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _batch = new List<AuditEvent>(_options.SqlBatchSize);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Audit logging is disabled");
            return;
        }

        var enabledSinks = _sinks.Where(s => s.IsEnabled).ToList();
        if (enabledSinks.Count == 0)
        {
            _logger.LogWarning("No audit sinks are enabled");
            return;
        }

        _logger.LogInformation("Audit background service started with {SinkCount} sink(s): {Sinks}",
            enabledSinks.Count, string.Join(", ", enabledSinks.Select(s => s.Name)));

        var flushTimer = new PeriodicTimer(TimeSpan.FromSeconds(_options.FlushIntervalSeconds));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Try to read events with timeout for periodic flush
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(TimeSpan.FromSeconds(_options.FlushIntervalSeconds));

                try
                {
                    while (await _auditLogger.Reader.WaitToReadAsync(cts.Token))
                    {
                        while (_auditLogger.Reader.TryRead(out var evt))
                        {
                            _batch.Add(evt);

                            if (_batch.Count >= _options.SqlBatchSize)
                            {
                                await FlushBatchAsync(enabledSinks, stoppingToken);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // Periodic flush timeout - flush any pending events
                    if (_batch.Count > 0)
                    {
                        await FlushBatchAsync(enabledSinks, stoppingToken);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown - flush remaining events
            _logger.LogInformation("Audit background service shutting down, flushing {Count} remaining events", _batch.Count);
        }
        finally
        {
            // Final flush on shutdown
            if (_batch.Count > 0)
            {
                await FlushBatchAsync(enabledSinks, CancellationToken.None);
            }

            _logger.LogInformation("Audit background service stopped");
        }
    }

    private async Task FlushBatchAsync(IEnumerable<IAuditSink> sinks, CancellationToken ct)
    {
        if (_batch.Count == 0) return;

        var events = _batch.ToList();
        _batch.Clear();

        foreach (var sink in sinks)
        {
            try
            {
                await WriteToBatchWithRetryAsync(sink, events, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write {Count} events to {Sink} sink after retries",
                    events.Count, sink.Name);
                // Continue to other sinks
            }
        }
    }

    private async Task WriteToBatchWithRetryAsync(IAuditSink sink, IReadOnlyList<AuditEvent> events, CancellationToken ct)
    {
        const int maxRetries = 3;
        var delay = TimeSpan.FromMilliseconds(100);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await sink.WriteBatchAsync(events, ct);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex,
                    "Attempt {Attempt}/{MaxRetries} to write to {Sink} failed, retrying in {Delay}ms",
                    attempt, maxRetries, sink.Name, delay.TotalMilliseconds);

                await Task.Delay(delay, ct);
                delay *= 2; // Exponential backoff
            }
        }
    }
}
