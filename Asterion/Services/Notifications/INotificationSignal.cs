namespace Asterion.Services.Notifications;

public interface INotificationSignal
{
    /// <summary>Wakes a waiting dispatcher immediately instead of it waiting out its poll interval.</summary>
    void Signal();

    Task WaitAsync(CancellationToken ct);
}
