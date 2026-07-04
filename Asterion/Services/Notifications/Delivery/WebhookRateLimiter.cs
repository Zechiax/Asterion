using System.Collections.Concurrent;

namespace Asterion.Services.Notifications.Delivery;

/// <summary>
///     A conservative artificial floor on top of Discord's own webhook rate limit (30 req/60s per webhook),
///     kept deliberately simple (fixed window per webhook URL) and dependency-free so it's cheaply unit
///     testable without any real time passing or HTTP involved.
/// </summary>
public class WebhookRateLimiter
{
    private readonly int _maxPerWindow;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, Window> _windows = new();

    public WebhookRateLimiter(int maxPerWindow = 25, TimeSpan? window = null)
    {
        _maxPerWindow = maxPerWindow;
        _window = window ?? TimeSpan.FromSeconds(60);
    }

    /// <summary>
    ///     Returns TimeSpan.Zero and records the send if one is allowed right now, otherwise returns how
    ///     long the caller should wait before trying again.
    /// </summary>
    public TimeSpan TryAcquire(string webhookUrl, DateTime nowUtc)
    {
        while (true)
        {
            var current = _windows.GetOrAdd(webhookUrl, _ => new Window(nowUtc, 0));

            if (nowUtc - current.StartedAt >= _window)
            {
                var reset = new Window(nowUtc, 1);
                if (_windows.TryUpdate(webhookUrl, reset, current))
                    return TimeSpan.Zero;
                continue;
            }

            if (current.Count < _maxPerWindow)
            {
                var incremented = current with { Count = current.Count + 1 };
                if (_windows.TryUpdate(webhookUrl, incremented, current))
                    return TimeSpan.Zero;
                continue;
            }

            return current.StartedAt + _window - nowUtc;
        }
    }

    private readonly record struct Window(DateTime StartedAt, int Count);
}
