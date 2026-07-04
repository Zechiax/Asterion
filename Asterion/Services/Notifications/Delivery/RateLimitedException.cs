namespace Asterion.Services.Notifications.Delivery;

/// <summary>Thrown when a sender's own rate-limit throttle blocks a send; carries the suggested retry delay.</summary>
public class RateLimitedException : Exception
{
    public TimeSpan RetryAfter { get; }

    public RateLimitedException(TimeSpan retryAfter)
        : base($"Rate limited, retry after {retryAfter}")
    {
        RetryAfter = retryAfter;
    }
}
