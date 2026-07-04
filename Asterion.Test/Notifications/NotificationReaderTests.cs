using Asterion.Database;
using Asterion.Database.Models;
using Asterion.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Asterion.Test.Notifications;

[TestFixture]
public class NotificationReaderTests
{
    private static PendingNotification MakePending(ulong entryId = 1)
    {
        return new PendingNotification
        {
            ProjectId = "project",
            VersionId = "version",
            SerializedProject = "{}",
            SerializedVersion = "{}",
            GuildId = 100,
            EntryId = entryId,
            Status = NotificationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    [Test]
    public async Task GetDueNotificationsReturnsPendingRows()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            db.PendingNotifications.Add(MakePending());
            await db.SaveChangesAsync();
        }

        var reader = new NotificationReader(provider);
        var due = await reader.GetDueNotificationsAsync(50, DateTime.UtcNow);

        Assert.That(due, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetDueNotificationsExcludesFailedRowsNotYetDueForRetry()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var row = MakePending();
            row.Status = NotificationStatus.Failed;
            row.NextRetryAt = DateTime.UtcNow.AddMinutes(30);
            db.PendingNotifications.Add(row);
            await db.SaveChangesAsync();
        }

        var reader = new NotificationReader(provider);
        var due = await reader.GetDueNotificationsAsync(50, DateTime.UtcNow);

        Assert.That(due, Is.Empty);
    }

    [Test]
    public async Task GetDueNotificationsIncludesFailedRowsPastRetryTime()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var row = MakePending();
            row.Status = NotificationStatus.Failed;
            row.NextRetryAt = DateTime.UtcNow.AddMinutes(-1);
            db.PendingNotifications.Add(row);
            await db.SaveChangesAsync();
        }

        var reader = new NotificationReader(provider);
        var due = await reader.GetDueNotificationsAsync(50, DateTime.UtcNow);

        Assert.That(due, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task MarkSentAsyncTransitionsRowToSent()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        int id;
        using (var scope = provider.CreateScope())
        {
            await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var row = MakePending();
            db.PendingNotifications.Add(row);
            await db.SaveChangesAsync();
            id = row.Id;
        }

        var reader = new NotificationReader(provider);
        await reader.MarkSentAsync(id);

        using var verifyScope = provider.CreateScope();
        await using var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DataContext>();
        var updated = await verifyDb.PendingNotifications.FindAsync(id);

        Assert.That(updated!.Status, Is.EqualTo(NotificationStatus.Sent));
    }

    [Test]
    public async Task MarkFailedAsyncIncrementsAttemptsAndSchedulesRetryInPlace()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        int id;
        using (var scope = provider.CreateScope())
        {
            await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var row = MakePending();
            db.PendingNotifications.Add(row);
            await db.SaveChangesAsync();
            id = row.Id;
        }

        var reader = new NotificationReader(provider);
        await reader.MarkFailedAsync(id, "boom", TimeSpan.FromMinutes(5));

        using var verifyScope = provider.CreateScope();
        await using var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DataContext>();
        var updated = await verifyDb.PendingNotifications.FindAsync(id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(updated!.Status, Is.EqualTo(NotificationStatus.Failed));
            Assert.That(updated.AttemptCount, Is.EqualTo(1));
            Assert.That(updated.LastError, Is.EqualTo("boom"));
            Assert.That(updated.NextRetryAt, Is.Not.Null);
            // Same row updated in place - never a second row created for the retry
            Assert.That(await EntityFrameworkQueryableExtensions.CountAsync(verifyDb.PendingNotifications), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task MarkDeadLetteredAsyncSetsTerminalStatus()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        int id;
        using (var scope = provider.CreateScope())
        {
            await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var row = MakePending();
            row.AttemptCount = 3;
            db.PendingNotifications.Add(row);
            await db.SaveChangesAsync();
            id = row.Id;
        }

        var reader = new NotificationReader(provider);
        await reader.MarkDeadLetteredAsync(id, "gave up");

        using var verifyScope = provider.CreateScope();
        await using var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DataContext>();
        var updated = await verifyDb.PendingNotifications.FindAsync(id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(updated!.Status, Is.EqualTo(NotificationStatus.DeadLettered));
            Assert.That(updated.AttemptCount, Is.EqualTo(4));
        }

        // Dead-lettered rows are terminal - no longer picked up by the dispatcher
        var due = await reader.GetDueNotificationsAsync(50, DateTime.UtcNow);
        Assert.That(due, Is.Empty);
    }
}
