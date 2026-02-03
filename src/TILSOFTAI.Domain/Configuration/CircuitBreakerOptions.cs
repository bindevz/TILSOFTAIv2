using System;

namespace TILSOFTAI.Domain.Configuration;

/// <summary>
/// Configuration options for a circuit breaker.
/// </summary>
public class CircuitBreakerOptions
{
    /// <summary>
    /// The number of exceptions or handled results that will break the circuit.
    /// Default is 5.
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// The duration of the time window during which failures are counted.
    /// Default is 30 seconds.
    /// </summary>
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The duration the circuit will stay open before resetting.
    /// Default is 30 seconds.
    /// </summary>
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The number of successful executions required to close the circuit when in HalfOpen state.
    /// In HalfOpen state, this number of actions are permitted.
    /// Default is 3.
    /// </summary>
    public int HalfOpenMaxAttempts { get; set; } = 3;
}
