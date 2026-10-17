namespace Asterion.Services.Notifications;

public interface INotificationSignal
{
    /// <summary>Wakes a waiting dispatcher immediately instead of it waiting out its poll interval.</summary>
    void Signal();

    /// <summary>Waits until signalled or until the timeout elapses. Returns true if signalled.</summary>
    Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct);
}
