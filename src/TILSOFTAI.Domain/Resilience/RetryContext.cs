using System;

namespace TILSOFTAI.Domain.Resilience;

public class RetryContext
{
    public int AttemptNumber { get; init; }
    public int TotalAttempts { get; init; }
    public Exception LastException { get; init; }
    public TimeSpan ElapsedTime { get; init; }
    public TimeSpan NextDelay { get; init; }

    public RetryContext(int attemptNumber, Exception lastException, TimeSpan nextDelay)
    {
        AttemptNumber = attemptNumber;
        LastException = lastException;
        NextDelay = nextDelay;
    }
}
