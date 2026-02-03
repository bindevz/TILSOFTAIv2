using System;
using System.Collections.Generic;

namespace TILSOFTAI.Domain.Configuration;

public class RetryOptions
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(5);
    public double BackoffMultiplier { get; set; } = 2.0;
    public double JitterFactor { get; set; } = 0.2;
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    // List of exceptions / status codes is tricky to configure via simple JSON binding
    // usually handled by code or complex object binding.
    // Spec says "RetryableExceptions: Type[]". This is hard to bind from JSON.
    // Spec says "RetryableStatusCodes: int[]". This works.
    public int[] RetryableStatusCodes { get; set; } = Array.Empty<int>();
}
