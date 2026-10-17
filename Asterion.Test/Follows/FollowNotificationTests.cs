using System.Text.Json;
using Asterion.Database;
using Asterion.Database.Models;
using Asterion.Services.Notifications;
using Asterion.Services.Notifications.Delivery;
using Asterion.Test.Fakes;
using Asterion.Test.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Modrinth;
using Modrinth.Models;
using NSubstitute;
using static Asterion.Test.Follows.FollowTestData;

namespace Asterion.Test.Follows;

[TestFixture]
public class FollowNotificationTests
{
    private const string ProjectId = "p";

    private static ModrinthFollow MakeFollow(int id, ulong guildId,
        FollowEvents events = FollowEvents.ProjectUpdate | FollowEvents.NewProject,
        ReleaseType releaseFilter = ReleaseType.Alpha | ReleaseType.Beta | ReleaseType.Release)
    {
        return new ModrinthFollow
        {
            Id = id,
            GuildId = guildId,
            TargetType = FollowTargetType.User,
            TargetId = $"user-{id}",
            TargetName = "user",
            Events = events,
            ReleaseFilter = releaseFilter,
            CustomUpdateChannel = 555,
            Created = DateTime.UtcNow
        };
    }

    private static DataContext OpenDb(IServiceProvider provider)
    {
        return provider.CreateScope().ServiceProvider.GetRequiredService<DataContext>();
    }

    [Test]
    public void FilterEvaluatorWorksWithFollow()
    {
        var version = MakeVersion("v", ProjectId, DateTime.UtcNow);
        var follow = MakeFollow(1, 1, releaseFilter: ReleaseType.Alpha);

        Assert.That(NotificationFilterEvaluator.ShouldNotify(follow, version), Is.False);

        follow.ReleaseFilter = ReleaseType.Release;
        follow.LoaderFilter = ["forge"];
        Assert.That(NotificationFilterEvaluator.ShouldNotify(follow, version), Is.False);

        follow.LoaderFilter = ["fabric"];
        Assert.That(NotificationFilterEvaluator.ShouldNotify(follow, version), Is.True);
    }

    [Test]
    public async Task ManualEntryWinsOverFollowAndTwoFollowsGiveOneRow()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await using var db = OpenDb(provider);

        var project = MakeProject(ProjectId, DateTime.UtcNow);
        var version = MakeVersion("v", ProjectId, DateTime.UtcNow);

        var follows = new[]
        {
            MakeFollow(1, guildId: 10), // guild 10 has a manual entry
            MakeFollow(2, guildId: 20),
            MakeFollow(3, guildId: 20),
            MakeFollow(4, guildId: 30, releaseFilter: ReleaseType.Alpha), // filtered out
            MakeFollow(5, guildId: 40, events: FollowEvents.NewProject) // no update event
        };

        new NotificationScheduler().EnqueueForFollows(db, project, version, follows, new HashSet<ulong> { 10 });
        await db.SaveChangesAsync();

        var rows = await db.PendingNotifications.ToListAsync();
        Assert.That(rows, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows[0].GuildId, Is.EqualTo(20u));
            Assert.That(rows[0].FollowId, Is.EqualTo(2));
            Assert.That(rows[0].EntryId, Is.Null);
            Assert.That(rows[0].Kind, Is.EqualTo(NotificationKind.Update));
        }
    }

    [Test]
    public async Task IgnoredProjectIsNotAFollowTarget()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        await SeedGuildAsync(provider, 2);

        await using (var db = OpenDb(provider))
        {
            db.ModrinthProjects.Add(new ModrinthProject
                { ProjectId = ProjectId, LastCheckVersion = "v0", Created = DateTime.UtcNow });
            var watching = MakeFollow(1, GuildId);
            var ignoring = MakeFollow(2, 2);
            var noUpdates = MakeFollow(3, 2, events: FollowEvents.NewProject);
            noUpdates.TargetId = "other";
            db.ModrinthFollows.AddRange(watching, ignoring, noUpdates);
            db.FollowProjects.AddRange(
                new FollowProject { FollowId = 1, ProjectId = ProjectId, FirstSeen = DateTime.UtcNow },
                new FollowProject { FollowId = 2, ProjectId = ProjectId, FirstSeen = DateTime.UtcNow, Ignored = true },
                new FollowProject { FollowId = 3, ProjectId = ProjectId, FirstSeen = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        await using var db2 = OpenDb(provider);
        var targets = await UpdateDetectionService.GetFollowTargetsAsync(db2, ProjectId);

        Assert.That(targets.Select(f => f.Id), Is.EqualTo(new[] { 1 }));
    }

    private static async Task<int> SeedFollowNotificationAsync(IServiceProvider provider, NotificationKind kind,
        ulong? customPingRole, ulong? newProjectPingRole, ulong? guildPingRole)
    {
        await SeedGuildAsync(provider);

        await using var db = OpenDb(provider);
        var guild = await db.Guilds.SingleAsync();
        guild.PingRole = guildPingRole;

        var follow = MakeFollow(1, GuildId);
        follow.CustomPingRole = customPingRole;
        follow.NewProjectPingRole = newProjectPingRole;

        db.ModrinthProjects.Add(new ModrinthProject
            { ProjectId = ProjectId, LastCheckVersion = "v0", Created = DateTime.UtcNow });
        db.ModrinthFollows.Add(follow);

        var row = new PendingNotification
        {
            ProjectId = ProjectId,
            VersionId = "v",
            SerializedProject = JsonSerializer.Serialize(MakeProject(ProjectId, DateTime.UtcNow)),
            SerializedVersion = JsonSerializer.Serialize(MakeVersion("v", ProjectId, DateTime.UtcNow)),
            GuildId = GuildId,
            FollowId = follow.Id,
            Kind = kind,
            Status = NotificationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.PendingNotifications.Add(row);
        await db.SaveChangesAsync();

        return row.Id;
    }

    private static NotificationDispatchService BuildDispatch(IServiceProvider provider,
        RecordingNotificationSender sender)
    {
        var modrinthClient = Substitute.For<IModrinthClient>();
        modrinthClient.Team.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(System.Array.Empty<TeamMember>());

        return new NotificationDispatchService(provider, new NotificationReader(provider), new NotificationSignal(),
            new NotificationSenderRouter([sender]), modrinthClient,
            NullLogger<NotificationDispatchService>.Instance);
    }

    [Test]
    public async Task FollowUpdateIsDeliveredToFollowChannelWithFollowPingRole()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedFollowNotificationAsync(provider, NotificationKind.Update, customPingRole: 7,
            newProjectPingRole: 8, guildPingRole: 9);
        var sender = new RecordingNotificationSender();

        await BuildDispatch(provider, sender).ProcessDueNotificationsAsync();

        Assert.That(sender.Sent, Has.Count.EqualTo(1));
        var (target, content) = sender.Sent[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(target, Is.EqualTo(new BotDeliveryTarget(GuildId, 555)));
            Assert.That(content.PingText, Is.EqualTo("<@&7>"));
            Assert.That(content.Embed.Title, Does.Contain("has been updated"));
        }
    }

    [TestCase(7ul, 8ul, 9ul, "<@&8>")]
    [TestCase(7ul, null, 9ul, "<@&7>")]
    [TestCase(null, null, 9ul, "<@&9>")]
    [TestCase(null, null, null, "")]
    public async Task NewProjectUsesNewProjectEmbedAndPingRoleOrder(ulong? customPingRole, ulong? newProjectPingRole,
        ulong? guildPingRole, string expectedPing)
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        var id = await SeedFollowNotificationAsync(provider, NotificationKind.NewProject, customPingRole,
            newProjectPingRole, guildPingRole);
        var sender = new RecordingNotificationSender();

        await BuildDispatch(provider, sender).ProcessDueNotificationsAsync();

        Assert.That(sender.Sent, Has.Count.EqualTo(1));
        var content = sender.Sent[0].Content;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(content.PingText, Is.EqualTo(expectedPing));
            Assert.That(content.Embed.Title, Does.StartWith("New project"));
        }

        await using var db = OpenDb(provider);
        Assert.That((await db.PendingNotifications.FindAsync(id))!.Status, Is.EqualTo(NotificationStatus.Sent));
    }

    [TestCase(NotificationKind.NewProject, 666ul, 666ul)]
    [TestCase(NotificationKind.Update, 666ul, 555ul)]
    [TestCase(NotificationKind.NewProject, null, 555ul)]
    public async Task NewProjectsGoToTheirOwnChannelIfSet(NotificationKind kind, ulong? newProjectChannel,
        ulong expectedChannel)
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedFollowNotificationAsync(provider, kind, null, null, null);
        await using (var db = OpenDb(provider))
        {
            var follow = await db.ModrinthFollows.SingleAsync();
            follow.NewProjectChannel = newProjectChannel;
            await db.SaveChangesAsync();
        }

        var sender = new RecordingNotificationSender();
        await BuildDispatch(provider, sender).ProcessDueNotificationsAsync();

        Assert.That(sender.Sent.Single().Target, Is.EqualTo(new BotDeliveryTarget(GuildId, expectedChannel)));
    }

    [Test]
    public async Task MissingFollowDeadLettersImmediately()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        int id;
        await using (var db = OpenDb(provider))
        {
            var row = new PendingNotification
            {
                ProjectId = ProjectId,
                VersionId = "v",
                SerializedProject = "{}",
                SerializedVersion = "{}",
                GuildId = GuildId,
                FollowId = 42,
                Status = NotificationStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            db.PendingNotifications.Add(row);
            await db.SaveChangesAsync();
            id = row.Id;
        }

        var sender = new RecordingNotificationSender();
        await BuildDispatch(provider, sender).ProcessDueNotificationsAsync();

        await using var db2 = OpenDb(provider);
        Assert.That(sender.Sent, Is.Empty);
        Assert.That((await db2.PendingNotifications.FindAsync(id))!.Status, Is.EqualTo(NotificationStatus.DeadLettered));
    }
}
