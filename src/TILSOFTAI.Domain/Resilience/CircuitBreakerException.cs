using System;
using TILSOFTAI.Domain.Errors;

namespace TILSOFTAI.Domain.Resilience;

/// <summary>
/// Exception thrown when a circuit breaker execution is rejected because the circuit is open.
/// </summary>
public class CircuitBreakerException : TilsoftApiException
{
    public string CircuitName { get; }

    public CircuitBreakerException(string circuitName, string message, Exception? innerException = null) 
        : base(ErrorCode.CircuitOpen, 503, message, innerException)
    {
        CircuitName = circuitName;
    }
}
