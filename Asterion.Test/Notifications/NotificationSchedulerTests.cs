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
            Guild = new Guild { GuildId = guildId, GuildSettings = new GuildSettings() },
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
    public async Task EnqueuesOneMilestonePerEnabledGuild()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        using var scope = provider.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        Guild MakeGuild(ulong id, bool enabled, MilestoneLadder ladder)
        {
            return new Guild
            {
                GuildId = id,
                GuildSettings = new GuildSettings { MilestonesEnabled = enabled, MilestoneLadder = ladder }
            };
        }

        ModrinthEntry Entry(ulong entryId, Guild guild, bool notify = true)
        {
            var entry = MakeEntry(entryId, guild.GuildId, ReleaseType.Release);
            entry.Guild = guild;
            entry.NotifyMilestones = notify;
            return entry;
        }

        ModrinthFollow Follow(int id, Guild guild)
        {
            return new ModrinthFollow
            {
                Id = id, GuildId = guild.GuildId, Guild = guild, TargetId = "user", TargetName = "user",
                Events = FollowEvents.All
            };
        }

        var standard = MakeGuild(100, true, MilestoneLadder.Standard);
        var round = MakeGuild(200, true, MilestoneLadder.Round);
        var disabled = MakeGuild(300, false, MilestoneLadder.Standard);
        var optedOut = MakeGuild(400, true, MilestoneLadder.Standard);
        var major = MakeGuild(500, true, MilestoneLadder.Major);

        var project = new Project { Id = "project", Title = "Test Project", Team = "team-1", Downloads = 10_500 };

        var added = new NotificationScheduler().EnqueueMilestone(db, project, 9_000, 10_500,
            [Entry(1, standard), Entry(3, disabled), Entry(4, optedOut, notify: false)],
            [Follow(1, standard), Follow(2, round), Follow(4, optedOut), Follow(5, major)]);
        await db.SaveChangesAsync();

        var rows = await db.PendingNotifications.OrderBy(r => r.GuildId).ToListAsync();
        Assert.That(added, Is.EqualTo(rows.Count));

        // The entry wins in its guild, an opted out entry blocks its guild's follow, Major has no 10k milestone
        Assert.That(rows.Select(r => (r.GuildId, r.EntryId, r.FollowId, r.Milestone)), Is.EqualTo(new[]
        {
            (100UL, (ulong?)1UL, (int?)null, (long?)10_000L),
            (200UL, null, 2, 10_000L)
        }));
        Assert.That(rows.All(r => r.Kind == NotificationKind.DownloadMilestone && r.VersionId == null));
    }

    [Test]
    public async Task EnqueueAppliesGuildDefaultFiltersWhenEntryAndFollowHaveNoOverride()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await using var db = InMemoryDb.OpenDb(provider);

        var project = new Project { Id = "p", Title = "P", Team = "t" };
        var beta = new Version
            { Id = "v", ProjectId = "p", ProjectVersionType = ProjectVersionType.Beta, Loaders = ["fabric"] };

        var entry = new ModrinthEntry
        {
            EntryId = 1, ProjectId = "p", GuildId = 100,
            Guild = new Guild { GuildId = 100, GuildSettings = new GuildSettings { DefaultReleaseFilter = ReleaseType.Release } }
        };
        var follow = new ModrinthFollow
        {
            Id = 2, GuildId = 200, TargetId = "u", TargetName = "u", Events = FollowEvents.ProjectUpdate,
            Guild = new Guild { GuildId = 200, GuildSettings = new GuildSettings { DefaultReleaseFilter = ReleaseType.Beta } }
        };

        var scheduler = new NotificationScheduler();
        scheduler.EnqueueForEntries(db, project, beta, [entry]);
        scheduler.EnqueueForFollows(db, project, beta, [follow], new HashSet<ulong>());

        Assert.That(db.PendingNotifications.Local.Select(r => (r.GuildId, r.FollowId)),
            Is.EqualTo(new[] { (200UL, (int?)2) }));
    }
}
