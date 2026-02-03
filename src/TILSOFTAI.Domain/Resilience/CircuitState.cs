namespace TILSOFTAI.Domain.Resilience;

/// <summary>
/// Represents the state of a circuit breaker.
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// The circuit is closed and operating normally.
    /// </summary>
    Closed,

    /// <summary>
    /// The circuit is open and failing fast.
    /// </summary>
    Open,

    /// <summary>
    /// The circuit is half-open and allowing probing requests.
    /// </summary>
    HalfOpen,

    /// <summary>
    /// The circuit is manually isolated (forced open).
    /// </summary>
    Isolated
}
