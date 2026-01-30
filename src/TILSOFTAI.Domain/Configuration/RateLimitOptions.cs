namespace TILSOFTAI.Domain.Configuration;

/// <summary>
/// Configuration for API rate limiting.
/// Controls the number of requests allowed per time window to prevent abuse.
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>
    /// Maximum number of requests allowed in the time window.
    /// Default: 100 requests.
    /// </summary>
    public int PermitLimit { get; set; } = 100;
    
    /// <summary>
    /// Time window in seconds for rate limiting.
    /// Default: 60 seconds (1 minute).
    /// </summary>
    public int WindowSeconds { get; set; } = 60;
    
    /// <summary>
    /// Maximum number of queued requests when limit is reached.
    /// Requests beyond this will be rejected immediately.
    /// Default: 2 requests.
    /// </summary>
    public int QueueLimit { get; set; } = 2;
}
