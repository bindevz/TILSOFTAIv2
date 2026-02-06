using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Orchestration.Analytics;

namespace TILSOFTAI.Infrastructure.Caching;

/// <summary>
/// PATCH 31.05: Background service that processes cache write queue.
/// Replaces fire-and-forget pattern with reliable background processing.
/// </summary>
public sealed class CacheWriteBackgroundService : BackgroundService, ICacheWriteQueue
{
    private readonly Channel<CacheWriteItem> _channel;
    private readonly AnalyticsCache _cache;
    private readonly IMetricsService _metrics;
    private readonly ILogger<CacheWriteBackgroundService> _logger;

    public CacheWriteBackgroundService(
        AnalyticsCache cache,
        IMetricsService metrics,
        ILogger<CacheWriteBackgroundService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Bounded channel: nếu queue đầy, drop item cũ nhất (stale data)
        _channel = Channel.CreateBounded<CacheWriteItem>(
            new BoundedChannelOptions(capacity: 256)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,   // Chỉ 1 consumer
                SingleWriter = false   // Nhiều producers (concurrent requests)
            });
    }

    /// <summary>Non-blocking enqueue. Returns false if channel is completed.</summary>
    public bool TryEnqueue(CacheWriteItem item)
    {
        if (!_channel.Writer.TryWrite(item))
        {
            _logger.LogWarning("CacheWriteQueue full, dropping oldest item");
            _metrics.IncrementCounter(MetricNames.CacheWriteDroppedTotal);
            return false;
        }
        return true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CacheWriteBackgroundService started");

        await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await _cache.SetAsync(
                    item.TenantId, 
                    item.NormalizedQuery, 
                    item.Roles, 
                    item.Insight, 
                    stoppingToken);

                _metrics.IncrementCounter(MetricNames.CacheWriteSuccessTotal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "CacheWriteFailed | Tenant: {TenantId} | Query: {Query} | Error: {Error}",
                    item.TenantId, 
                    item.NormalizedQuery?.Length > 50 
                        ? item.NormalizedQuery[..50] + "..." 
                        : item.NormalizedQuery,
                    ex.Message);

                _metrics.IncrementCounter(MetricNames.CacheWriteFailuresTotal);
                // Do NOT rethrow — continue processing next item
            }
        }

        _logger.LogInformation("CacheWriteBackgroundService stopped");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.Complete();
        await base.StopAsync(cancellationToken);
    }
}
