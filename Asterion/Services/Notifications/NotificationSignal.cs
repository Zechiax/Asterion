namespace Asterion.Services.Notifications;

/// <summary>
///     Single-shard, single-process bot - in-process semaphore is all the coordination needed here.
/// </summary>
public class NotificationSignal : INotificationSignal
{
    private readonly SemaphoreSlim _semaphore = new(0, 1);

    public void Signal()
    {
        // Avoid queueing up releases beyond 1 - the dispatcher only needs to know "something is due", not how many times
        if (_semaphore.CurrentCount == 0)
            _semaphore.Release();
    }

    public Task WaitAsync(CancellationToken ct) => _semaphore.WaitAsync(ct);
}
