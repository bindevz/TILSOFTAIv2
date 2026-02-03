using System;
using System.Threading;
using System.Threading.Tasks;

namespace TILSOFTAI.Domain.Resilience;

/// <summary>
/// Defines a policy for circuit breaking to protect external dependencies.
/// </summary>
public interface ICircuitBreakerPolicy
{
    /// <summary>
    /// Gets the current state of the circuit.
    /// </summary>
    CircuitState State { get; }

    /// <summary>
    /// Gets the name of the circuit breaker.
    /// </summary>
    string CircuitName { get; }

    /// <summary>
    /// Executes the specified action within the circuit breaker policy.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="action">The action to execute.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The result of the action.</returns>
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
}
