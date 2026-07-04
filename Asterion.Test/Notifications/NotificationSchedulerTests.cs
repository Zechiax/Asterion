using Asterion.Database;
using Asterion.Database.Models;
using Asterion.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modrinth.Models;
using Modrinth.Models.Enums.Project;
using Version = Modrinth.Models.Version;

namespace Asterion.Test.Notifications;

[TestFixture]
public class NotificationSchedulerTests
{
    private static ModrinthEntry MakeEntry(ulong entryId, ulong guildId, ReleaseType releaseFilter)
    {
        return new ModrinthEntry
        {
            EntryId = entryId,
            ArrayId = 1,
            ProjectId = "project",
            GuildId = guildId,
            Created = DateTime.UtcNow,
            ReleaseFilter = releaseFilter
        };
    }

    [Test]
    public async Task EnqueuesOneRowPerPassingEntry()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        using var scope = provider.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var project = new Project { Id = "project", Title = "Test Project", Team = "team-1" };
        var version = new Version
        {
            Id = "version-1", ProjectId = "project", ProjectVersionType = ProjectVersionType.Release,
            Loaders = [], DatePublished = DateTime.UtcNow
        };

        var passingEntry = MakeEntry(1, 100, ReleaseType.Release);
        var filteredEntry = MakeEntry(2, 200, ReleaseType.Alpha); // won't match a Release version

        var scheduler = new NotificationScheduler();
        scheduler.EnqueueForEntries(db, project, version, [passingEntry, filteredEntry]);
        await db.SaveChangesAsync();

        var rows = await db.PendingNotifications.ToListAsync();

        Assert.That(rows, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows[0].EntryId, Is.EqualTo(1u));
            Assert.That(rows[0].GuildId, Is.EqualTo(100u));
            Assert.That(rows[0].ProjectId, Is.EqualTo("project"));
            Assert.That(rows[0].VersionId, Is.EqualTo("version-1"));
            Assert.That(rows[0].Status, Is.EqualTo(NotificationStatus.Pending));
            Assert.That(rows[0].AttemptCount, Is.EqualTo(0));
        }
    }

    [Test]
    public async Task EnqueuesNothingWhenNoEntryPassesTheFilter()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        using var scope = provider.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var project = new Project { Id = "project", Title = "Test Project", Team = "team-1" };
        var version = new Version
        {
            Id = "version-1", ProjectId = "project", ProjectVersionType = ProjectVersionType.Alpha,
            Loaders = [], DatePublished = DateTime.UtcNow
        };

        var entry = MakeEntry(1, 100, ReleaseType.Release);

        var scheduler = new NotificationScheduler();
        scheduler.EnqueueForEntries(db, project, version, [entry]);
        await db.SaveChangesAsync();

        Assert.That(await EntityFrameworkQueryableExtensions.CountAsync(db.PendingNotifications), Is.EqualTo(0));
    }
}
