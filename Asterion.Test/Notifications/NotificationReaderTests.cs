using Asterion.Database;
using Asterion.Database.Models;
using Asterion.Services.Notifications;
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

    // Pending rows are due right away, failed ones only once their retry time has passed
    [TestCase(NotificationStatus.Pending, null, true)]
    [TestCase(NotificationStatus.Failed, 30, false)]
    [TestCase(NotificationStatus.Failed, -1, true)]
    public async Task GetDueNotificationsRespectsRetryTime(NotificationStatus status, int? retryInMinutes, bool due)
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var row = MakePending();
            row.Status = status;
            row.NextRetryAt = retryInMinutes is { } minutes ? DateTime.UtcNow.AddMinutes(minutes) : null;
            db.PendingNotifications.Add(row);
            await db.SaveChangesAsync();
        }

        var reader = new NotificationReader(provider);
        var rows = await reader.GetDueNotificationsAsync(50, DateTime.UtcNow);

        Assert.That(rows, Has.Count.EqualTo(due ? 1 : 0));
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
