using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Domain.Resilience;
using TILSOFTAI.Infrastructure.Resilience;
using Xunit;

namespace TILSOFTAI.Tests.Unit.Resilience;

public class CircuitBreakerPolicyTests
{
    private readonly Mock<IMetricsService> _metricsMock;
    private readonly CircuitBreakerOptions _options;

    public CircuitBreakerPolicyTests()
    {
        _metricsMock = new Mock<IMetricsService>();
        _options = new CircuitBreakerOptions
        {
            FailureThreshold = 2,
            SamplingDuration = TimeSpan.FromSeconds(1), // Short window for testing
            BreakDuration = TimeSpan.FromMilliseconds(500), // Short break
            HalfOpenMaxAttempts = 1
        };
    }

    [Fact]
    public async Task ExecuteAsync_Should_Open_Circuit_After_Threshold_Failures()
    {
        // Arrange
        // For AdvancedCircuitBreaker with 0.5 threshold and minThroughput 2:
        // We need 2 failures out of 2 executions (100% failure rate) to trip > 0.5? Or exactly 50%?
        // FailureThreshold is 0.5 (50%), so > 50% failures opens it.
        // If we execute 2 times and fail both, that's 100%.
        var policy = new PollyCircuitBreakerPolicy("test", _options, NullLogger.Instance, _metricsMock.Object);

        // Act & Assert
        // 1st failure
        await Assert.ThrowsAsync<Exception>(() => policy.ExecuteAsync<string>(ct => throw new Exception("fail"), CancellationToken.None));
        
        // 2nd failure
        await Assert.ThrowsAsync<Exception>(() => policy.ExecuteAsync<string>(ct => throw new Exception("fail"), CancellationToken.None));

        // Circuit should be open now or on next call?
        // Polly Advanced Circuit Breaker usually opens *after* the condition is met.
        // Let's verify status.
        // Wait a tiny bit for Polly to process stats? Usually synchronous for breakdown.
        
        // 3rd call should throw CircuitBreakerException
        await Assert.ThrowsAsync<CircuitBreakerException>(() => policy.ExecuteAsync<string>(ct => Task.FromResult("success"), CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_Should_Recover_After_BreakDuration()
    {
        // Arrange
        var policy = new PollyCircuitBreakerPolicy("test", _options, NullLogger.Instance, _metricsMock.Object);

        // Trip the circuit
        await Assert.ThrowsAsync<Exception>(() => policy.ExecuteAsync<string>(ct => throw new Exception("fail"), CancellationToken.None));
        await Assert.ThrowsAsync<Exception>(() => policy.ExecuteAsync<string>(ct => throw new Exception("fail"), CancellationToken.None));

        // Verify Open
        await Assert.ThrowsAsync<CircuitBreakerException>(() => policy.ExecuteAsync<string>(ct => Task.FromResult("s"), CancellationToken.None));

        // Act: Wait for break duration
        await Task.Delay(_options.BreakDuration.Add(TimeSpan.FromMilliseconds(200)));

        // Now it should be Half-Open, allowing a probe
        // Check state
        policy.State.Should().Be(CircuitState.HalfOpen);

        // Successful execution closes it
        await policy.ExecuteAsync<string>(ct => Task.FromResult("success"), CancellationToken.None);

        policy.State.Should().Be(CircuitState.Closed);
    }
}
