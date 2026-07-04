using Asterion.Database;
using Asterion.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Asterion.Services.Notifications;

public class NotificationReader : INotificationReader
{
    private static readonly TimeSpan[] BackoffSchedule =
    {
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(4)
    };

    private readonly IServiceProvider _services;

    public NotificationReader(IServiceProvider services)
    {
        _services = services;
    }

    public static TimeSpan BackoffFor(int attemptCount)
    {
        var index = Math.Min(attemptCount, BackoffSchedule.Length - 1);
        return BackoffSchedule[index];
    }

    public async Task<IReadOnlyList<PendingNotification>> GetDueNotificationsAsync(int batchSize, DateTime asOfUtc,
        CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        return await db.PendingNotifications
            .Where(p => p.Status == NotificationStatus.Pending
                        || (p.Status == NotificationStatus.Failed
                            && (p.NextRetryAt == null || p.NextRetryAt <= asOfUtc)))
            .OrderBy(p => p.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task MarkSentAsync(int id, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var row = await db.PendingNotifications.FindAsync([id], ct);
        if (row is null) return;

        row.Status = NotificationStatus.Sent;
        row.LastAttemptAt = DateTime.UtcNow;
        row.LastError = null;

        await db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(int id, string error, TimeSpan retryDelay, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var row = await db.PendingNotifications.FindAsync([id], ct);
        if (row is null) return;

        var now = DateTime.UtcNow;
        row.Status = NotificationStatus.Failed;
        row.AttemptCount++;
        row.LastError = error;
        row.LastAttemptAt = now;
        row.NextRetryAt = now + retryDelay;

        await db.SaveChangesAsync(ct);
    }

    public async Task MarkDeadLetteredAsync(int id, string error, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var row = await db.PendingNotifications.FindAsync([id], ct);
        if (row is null) return;

        row.Status = NotificationStatus.DeadLettered;
        row.AttemptCount++;
        row.LastError = error;
        row.LastAttemptAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }
}
