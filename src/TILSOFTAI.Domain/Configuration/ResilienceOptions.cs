namespace TILSOFTAI.Domain.Configuration;

/// <summary>
/// Global resilience configuration options.
/// </summary>
public class ResilienceOptions
{
    /// <summary>
    /// Master switch for resilience features.
    /// </summary>
    public bool GlobalEnabled { get; set; } = true;

    /// <summary>
    /// Circuit breaker options for LLM clients.
    /// </summary>
    public CircuitBreakerOptions LlmCircuitBreaker { get; set; } = new();

    /// <summary>
    /// Circuit breaker options for SQL database execution.
    /// </summary>
    public CircuitBreakerOptions SqlCircuitBreaker { get; set; } = new();

    /// <summary>
    /// Circuit breaker options for Redis cache.
    /// </summary>
    public CircuitBreakerOptions RedisCircuitBreaker { get; set; } = new();

    /// <summary>
    /// Retry options for LLM clients.
    /// </summary>
    public RetryOptions LlmRetry { get; set; } = new();

    /// <summary>
    /// Retry options for SQL database execution.
    /// </summary>
    public RetryOptions SqlRetry { get; set; } = new();

    /// <summary>
    /// Retry options for Redis cache.
    /// </summary>
    public RetryOptions RedisRetry { get; set; } = new();
}
