using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Domain.Resilience;
using TILSOFTAI.Infrastructure.Resilience;
using Xunit;

namespace TILSOFTAI.Tests.Unit.Resilience;

public class PollyRetryPolicyTests
{
    private readonly Mock<ILogger> _logger = new();
    private readonly Mock<IMetricsService> _metrics = new();

    [Fact]
    public async Task ExecuteAsync_ShouldReturnResultOnSuccess()
    {
        var options = new RetryOptions { MaxRetries = 2 };
        var policy = new PollyRetryPolicy("test", options, _logger.Object, _metrics.Object);
        var result = await policy.ExecuteAsync<string>(ct => Task.FromResult("success"), CancellationToken.None);
        Assert.Equal("success", result);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRetryOnTransientFailure()
    {
        var options = new RetryOptions { MaxRetries = 3, InitialDelay = TimeSpan.Zero };
        var policy = new PollyRetryPolicy("test", options, _logger.Object, _metrics.Object);

        int attempts = 0;
        var result = await policy.ExecuteAsync<string>(async ct =>
        {
            attempts++;
            if (attempts < 3) throw new TimeoutException();
            return "success";
        }, CancellationToken.None);

        Assert.Equal("success", result);
        Assert.Equal(3, attempts); // 1 initial + 2 retries
        
        // Verify metrics
        _metrics.Verify(m => m.IncrementCounter(MetricNames.RetryAttemptsTotal, It.IsAny<Dictionary<string, string>>(), 1), Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFailAfterMaxRetries()
    {
        var options = new RetryOptions { MaxRetries = 2, InitialDelay = TimeSpan.Zero };
        var policy = new PollyRetryPolicy("test", options, _logger.Object, _metrics.Object);

        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await policy.ExecuteAsync<string>(ct => throw new TimeoutException(), CancellationToken.None);
        });

        _metrics.Verify(m => m.IncrementCounter(MetricNames.RetryAttemptsTotal, It.IsAny<Dictionary<string, string>>(), 1), Times.Exactly(2));
    }
}
