using Asterion.Database;
using Asterion.Database.Models;
using Asterion.Interfaces;
using Asterion.Services.Follows;
using Asterion.Test.Fakes;
using Asterion.Test.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Modrinth.Models.Enums.Project;
using static Asterion.Test.Follows.FollowTestData;

namespace Asterion.Test.Follows;

[TestFixture]
public class FollowSyncTests
{
    private static readonly DateTime LongAgo = DateTime.UtcNow.AddYears(-1);

    private static async Task<int> AddFollowAsync(FollowService service)
    {
        var result = await service.AddFollowAsync(GuildId, FollowTargetType.User, UserId, "user", 555,
            FollowEvents.ProjectUpdate | FollowEvents.NewProject);

        Assert.That(result.Status, Is.EqualTo(AddFollowStatus.Added));
        return result.Follow!.Id;
    }

    private static DataContext OpenDb(IServiceProvider provider)
    {
        return provider.CreateScope().ServiceProvider.GetRequiredService<DataContext>();
    }

    [Test]
    public async Task FirstSyncTracksProjectsWithoutAnnouncing()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        var source = new FakeFollowSource();
        // Even a project approved "now" must not be announced on the first sync
        source.SetProjects(MakeProject("a", LongAgo), MakeProject("b", DateTime.UtcNow.AddMinutes(1)));
        var service = BuildService(provider, source);

        var result = await service.AddFollowAsync(GuildId, FollowTargetType.User, UserId, "user", 555,
            FollowEvents.ProjectUpdate | FollowEvents.NewProject);

        await using var db = OpenDb(provider);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Sync!.TrackedCount, Is.EqualTo(2));
            Assert.That(await db.FollowProjects.CountAsync(), Is.EqualTo(2));
            Assert.That(await db.ModrinthProjects.CountAsync(), Is.EqualTo(2));
            Assert.That(await db.PendingNotifications.CountAsync(), Is.EqualTo(0));
        }

        var project = await db.ModrinthProjects.SingleAsync(p => p.ProjectId == "a");
        Assert.That(project.LastCheckVersion, Is.EqualTo("a-v1"));
    }

    [Test]
    public async Task NewProjectIsTrackedAndAnnounced()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        var source = new FakeFollowSource();
        source.SetProjects(MakeProject("a", LongAgo));
        var service = BuildService(provider, source);
        var followId = await AddFollowAsync(service);

        source.SetProjects(MakeProject("a", LongAgo), MakeProject("b", DateTime.UtcNow.AddMinutes(1)));
        var result = await service.SyncFollowAsync(followId, announceNew: true);

        Assert.That(result!.Announced, Is.EqualTo(new[] { "b" }));

        await using var db = OpenDb(provider);
        var row = await db.PendingNotifications.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await db.FollowProjects.AnyAsync(fp => fp.ProjectId == "b"), Is.True);
            Assert.That(row.Kind, Is.EqualTo(NotificationKind.NewProject));
            Assert.That(row.FollowId, Is.EqualTo(followId));
            Assert.That(row.EntryId, Is.Null);
            Assert.That(row.ProjectId, Is.EqualTo("b"));
            Assert.That(row.VersionId, Is.EqualTo("b-v1"));
            Assert.That(row.GuildId, Is.EqualTo(GuildId));
        }
    }

    [Test]
    public async Task NewProjectIsNotAnnouncedWithoutNewProjectEvent()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        var source = new FakeFollowSource();
        var service = BuildService(provider, source);

        var add = await service.AddFollowAsync(GuildId, FollowTargetType.User, UserId, "user", 555,
            FollowEvents.ProjectUpdate);

        source.SetProjects(MakeProject("b", DateTime.UtcNow.AddMinutes(1)));
        await service.SyncFollowAsync(add.Follow!.Id, announceNew: true);

        await using var db = OpenDb(provider);
        Assert.That(await db.FollowProjects.CountAsync(), Is.EqualTo(1));
        Assert.That(await db.PendingNotifications.CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task ProjectApprovedBeforeFollowWasCreatedIsTrackedButNotAnnounced()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        var source = new FakeFollowSource();
        var service = BuildService(provider, source);
        var followId = await AddFollowAsync(service);

        // The user became a maintainer of an old project
        source.SetProjects(MakeProject("old", LongAgo));
        var result = await service.SyncFollowAsync(followId, announceNew: true);

        await using var db = OpenDb(provider);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.Added, Is.EqualTo(new[] { "old" }));
            Assert.That(result.Announced, Is.Empty);
            Assert.That(await db.PendingNotifications.CountAsync(), Is.EqualTo(0));
        }
    }

    [Test]
    public async Task SourceFailureChangesNoDataAndSetsLastSyncError()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        var source = new FakeFollowSource();
        source.SetProjects(MakeProject("a", LongAgo));
        var service = BuildService(provider, source);
        var followId = await AddFollowAsync(service);

        source.Result = FollowSourceResult.Failed("User not found");
        var result = await service.SyncFollowAsync(followId, announceNew: true);

        await using var db = OpenDb(provider);
        var follow = await db.ModrinthFollows.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.Success, Is.False);
            Assert.That(follow.LastSyncError, Is.EqualTo("User not found"));
            Assert.That(await db.FollowProjects.CountAsync(), Is.EqualTo(1));
            Assert.That(await db.ModrinthProjects.CountAsync(), Is.EqualTo(1));
        }

        // The next successful sync clears the error
        source.SetProjects(MakeProject("a", LongAgo));
        await service.SyncFollowAsync(followId, announceNew: true);

        await using var db2 = OpenDb(provider);
        Assert.That((await db2.ModrinthFollows.SingleAsync()).LastSyncError, Is.Null);
    }

    [Test]
    public async Task ProjectWithNoVersionsIsSkippedThenAddedOnNextSync()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        var source = new FakeFollowSource();
        source.SetProjects(MakeProject("a", LongAgo, hasVersions: false));
        var service = BuildService(provider, source);
        var followId = await AddFollowAsync(service);

        await using (var db = OpenDb(provider))
        {
            Assert.That(await db.FollowProjects.CountAsync(), Is.EqualTo(0));
            Assert.That(await db.ModrinthProjects.CountAsync(), Is.EqualTo(0));
        }

        source.SetProjects(MakeProject("a", LongAgo));
        var result = await service.SyncFollowAsync(followId, announceNew: true);

        Assert.That(result!.Added, Is.EqualTo(new[] { "a" }));
    }

    [Test]
    public async Task ProjectThatLeavesTheListLosesItsFollowProjectRow()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        var source = new FakeFollowSource();
        source.SetProjects(MakeProject("a", LongAgo), MakeProject("b", LongAgo));
        var service = BuildService(provider, source);
        var followId = await AddFollowAsync(service);

        source.SetProjects(MakeProject("a", LongAgo));
        var result = await service.SyncFollowAsync(followId, announceNew: true);

        await using var db = OpenDb(provider);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.Removed, Is.EqualTo(new[] { "b" }));
            Assert.That(await db.FollowProjects.Select(fp => fp.ProjectId).ToListAsync(), Is.EqualTo(new[] { "a" }));
            // Nothing else uses the project, so the shared state is cleaned up too
            Assert.That(await db.ModrinthProjects.AnyAsync(p => p.ProjectId == "b"), Is.False);
        }
    }

    [Test]
    public async Task ProjectThatBecomesPrivateIsRemoved()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        var source = new FakeFollowSource();
        source.SetProjects(MakeProject("a", LongAgo), MakeProject("u", LongAgo, ProjectStatus.Unlisted));
        var service = BuildService(provider, source);
        var followId = await AddFollowAsync(service);

        source.SetProjects(MakeProject("a", LongAgo, ProjectStatus.Private),
            MakeProject("u", LongAgo, ProjectStatus.Unlisted));
        var result = await service.SyncFollowAsync(followId, announceNew: true);

        Assert.That(result!.Removed, Is.EqualTo(new[] { "a" }));
        Assert.That(result.TrackedCount, Is.EqualTo(1));
    }

    [Test]
    public async Task ReturningProjectIsTrackedAgainWithoutAnnouncement()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        var source = new FakeFollowSource();
        var service = BuildService(provider, source);
        var followId = await AddFollowAsync(service);

        // Published after the follow was created, but long before the last sync
        var approved = DateTime.UtcNow.AddMinutes(1);
        source.SetProjects(MakeProject("a", approved));
        await service.SyncFollowAsync(followId, announceNew: true);

        source.SetProjects();
        await service.SyncFollowAsync(followId, announceNew: true);

        // Simulate the project coming back days later
        await using (var db = OpenDb(provider))
        {
            var follow = await db.ModrinthFollows.SingleAsync();
            follow.Created = approved.AddDays(-10);
            follow.LastSyncedAt = approved.AddDays(5);
            await db.SaveChangesAsync();
        }

        source.SetProjects(MakeProject("a", approved));
        var result = await service.SyncFollowAsync(followId, announceNew: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.Added, Is.EqualTo(new[] { "a" }));
            Assert.That(result.Announced, Is.Empty);
        }
    }

    [Test]
    public async Task AddFollowEnforcesLimitAndUniqueness()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        var service = BuildService(provider, new FakeFollowSource());

        for (var i = 0; i < IFollowService.MaxFollowsPerGuild; i++)
        {
            var added = await service.AddFollowAsync(GuildId, FollowTargetType.User, $"user-{i}", "user", null,
                FollowEvents.ProjectUpdate);
            Assert.That(added.Status, Is.EqualTo(AddFollowStatus.Added));
        }

        var duplicate = await service.AddFollowAsync(GuildId, FollowTargetType.User, "user-0", "user", null,
            FollowEvents.ProjectUpdate);
        var overLimit = await service.AddFollowAsync(GuildId, FollowTargetType.User, "user-new", "user", null,
            FollowEvents.ProjectUpdate);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(duplicate.Status, Is.EqualTo(AddFollowStatus.AlreadyFollowing));
            Assert.That(overLimit.Status, Is.EqualTo(AddFollowStatus.LimitReached));
        }
    }

    [Test]
    public async Task SyncPassCallsSourceOncePerTarget()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        await SeedGuildAsync(provider, 2);
        var source = new FakeFollowSource();
        source.SetProjects(MakeProject("a", LongAgo));
        var service = BuildService(provider, source);

        await service.AddFollowAsync(GuildId, FollowTargetType.User, UserId, "user", null, FollowEvents.NewProject);
        await service.AddFollowAsync(2, FollowTargetType.User, UserId, "user", null, FollowEvents.NewProject);
        var callsAfterAdd = source.CallCount;

        source.SetProjects(MakeProject("a", LongAgo), MakeProject("b", DateTime.UtcNow.AddMinutes(1)));
        await new FollowSyncService(service, NullLogger<FollowSyncService>.Instance).RunSyncPassAsync();

        await using var db = OpenDb(provider);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(source.CallCount - callsAfterAdd, Is.EqualTo(1));
            Assert.That(await db.PendingNotifications.CountAsync(), Is.EqualTo(2));
            // One shared project row, two followed project rows
            Assert.That(await db.ModrinthProjects.CountAsync(p => p.ProjectId == "b"), Is.EqualTo(1));
            Assert.That(await db.FollowProjects.CountAsync(fp => fp.ProjectId == "b"), Is.EqualTo(2));
        }
    }

    [Test]
    public async Task LoadersComeFromTheLastSyncWithoutCallingModrinth()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        var source = new FakeFollowSource();
        var project = MakeProject("a", LongAgo);
        project.Loaders = ["fabric", "Fabric", "quilt"];
        source.SetProjects(project);
        var service = BuildService(provider, source);

        await AddFollowAsync(service);
        var callsAfterAdd = source.CallCount;

        var loaders = await service.GetTargetLoadersAsync(FollowTargetType.User, UserId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaders, Is.EquivalentTo(new[] { "fabric", "quilt" }));
            Assert.That(source.CallCount, Is.EqualTo(callsAfterAdd));
        }
    }

    [Test]
    public async Task LoadersOfUnknownTargetAreFetchedOnce()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        var source = new FakeFollowSource();
        source.SetProjects(MakeProject("a", LongAgo));
        var service = BuildService(provider, source);

        await service.GetTargetLoadersAsync(FollowTargetType.User, UserId);
        var loaders = await service.GetTargetLoadersAsync(FollowTargetType.User, UserId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaders, Is.EqualTo(new[] { "fabric" }));
            Assert.That(source.CallCount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task UpdatedFollowKeepsItsProjects()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        var source = new FakeFollowSource();
        source.SetProjects(MakeProject("a", LongAgo), MakeProject("b", LongAgo));
        var service = BuildService(provider, source);
        var followId = await AddFollowAsync(service);
        await service.SetIgnoredAsync(GuildId, followId, "a", true);

        var updated = await service.UpdateSettingsAsync(GuildId, followId, f => f.CustomPingRole = 666);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(updated!.CustomPingRole, Is.EqualTo(666ul));
            Assert.That(updated.Projects, Has.Count.EqualTo(2));
            Assert.That(updated.Projects.Count(fp => fp.Ignored), Is.EqualTo(1));
        }
    }
}
