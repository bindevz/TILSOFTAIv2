using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Api.Auth;

public sealed class JwtSigningKeyRefreshHostedService : BackgroundService
{
    private readonly JwtSigningKeyProvider _provider;
    private readonly AuthOptions _authOptions;
    private readonly ILogger<JwtSigningKeyRefreshHostedService> _logger;

    public JwtSigningKeyRefreshHostedService(
        JwtSigningKeyProvider provider,
        IOptions<AuthOptions> authOptions,
        ILogger<JwtSigningKeyRefreshHostedService> logger)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _authOptions = authOptions?.Value ?? throw new ArgumentNullException(nameof(authOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await TryRefreshAsync(stoppingToken);

        var failureCount = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelay(failureCount);
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            var success = await TryRefreshAsync(stoppingToken);
            failureCount = success ? 0 : failureCount + 1;
        }
    }

    private async Task<bool> TryRefreshAsync(CancellationToken ct)
    {
        try
        {
            return await _provider.RefreshAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT signing key refresh loop encountered an error.");
            return false;
        }
    }

    private TimeSpan GetDelay(int failureCount)
    {
        var refreshInterval = Math.Max(1, _authOptions.JwksRefreshIntervalMinutes);
        if (failureCount <= 0)
        {
            return ApplyJitter(TimeSpan.FromMinutes(refreshInterval));
        }

        var baseBackoff = Math.Max(1, _authOptions.JwksRefreshFailureBackoffSeconds);
        var maxBackoff = Math.Max(baseBackoff, _authOptions.JwksRefreshMaxBackoffSeconds);
        var exponential = baseBackoff * Math.Pow(2, Math.Min(failureCount - 1, 6));
        var backoffSeconds = Math.Min(maxBackoff, exponential);
        return ApplyJitter(TimeSpan.FromSeconds(backoffSeconds));
    }

    private static TimeSpan ApplyJitter(TimeSpan baseDelay)
    {
        var jitterMs = Random.Shared.Next(0, 1000);
        return baseDelay + TimeSpan.FromMilliseconds(jitterMs);
    }
}
