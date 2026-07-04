using Asterion.Database.Models;

namespace Asterion.Services.Notifications;

public interface INotificationReader
{
    Task<IReadOnlyList<PendingNotification>> GetDueNotificationsAsync(int batchSize, DateTime asOfUtc,
        CancellationToken ct = default);

    Task MarkSentAsync(int id, CancellationToken ct = default);
    Task MarkFailedAsync(int id, string error, TimeSpan retryDelay, CancellationToken ct = default);
    Task MarkDeadLetteredAsync(int id, string error, CancellationToken ct = default);
}
