using Asterion.Database;
using Asterion.Database.Models;
using Asterion.Services.Subscriptions;
using Asterion.Test.Fakes;
using Asterion.Test.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static Asterion.Test.Follows.FollowTestData;
using static Asterion.Test.Notifications.InMemoryDb;

namespace Asterion.Test.Follows;

/// <summary>
///     Shared ModrinthProject rows must survive as long as an entry or a follow uses them.
/// </summary>
[TestFixture]
public class FollowCleanupTests
{
    private static readonly DateTime LongAgo = DateTime.UtcNow.AddYears(-1);

    [Test]
    public async Task ProjectUsedOnlyByFollowSurvivesUntilFollowIsRemoved()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        var source = new FakeFollowSource();
        source.SetProjects(MakeProject("a", LongAgo));
        var service = BuildService(provider, source);

        var add = await service.AddFollowAsync(GuildId, FollowTargetType.User, UserId, "user", null,
            FollowEvents.ProjectUpdate);

        await using (var db = OpenDb(provider))
        {
            var removed = await ProjectCleanup.RemoveUnusedProjectsAsync(db);
            Assert.That(removed, Is.Empty);
        }

        Assert.That(await service.RemoveFollowAsync(GuildId, add.Follow!.Id), Is.Not.Null);

        await using var db2 = OpenDb(provider);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await db2.ModrinthFollows.CountAsync(), Is.EqualTo(0));
            Assert.That(await db2.FollowProjects.CountAsync(), Is.EqualTo(0));
            Assert.That(await db2.ModrinthProjects.CountAsync(), Is.EqualTo(0));
        }
    }

    [Test]
    public async Task UnsubscribingKeepsProjectThatAFollowStillUses()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        var source = new FakeFollowSource();
        source.SetProjects(MakeProject("a", LongAgo));
        var followService = BuildService(provider, source);

        var subscriptionService = new SubscriptionService(provider, NullLogger<SubscriptionService>.Instance);

        await subscriptionService.AddSubscriptionAsync(GuildId, "a", "a-v1", LongAgo, 555, "Project a");
        await followService.AddFollowAsync(GuildId, FollowTargetType.User, UserId, "user", null,
            FollowEvents.ProjectUpdate);

        Assert.That(await subscriptionService.RemoveSubscriptionAsync(GuildId, "a"), Is.Not.Null);

        await using var db = OpenDb(provider);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await db.ModrinthEntries.CountAsync(), Is.EqualTo(0));
            Assert.That(await db.ModrinthProjects.AnyAsync(p => p.ProjectId == "a"), Is.True);
        }
    }

    [Test]
    public async Task RemovingFollowKeepsProjectThatAnEntryStillUses()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        var source = new FakeFollowSource();
        source.SetProjects(MakeProject("a", LongAgo));
        var followService = BuildService(provider, source);

        var subscriptionService = new SubscriptionService(provider, NullLogger<SubscriptionService>.Instance);

        await subscriptionService.AddSubscriptionAsync(GuildId, "a", "a-v1", LongAgo, 555, "Project a");
        var add = await followService.AddFollowAsync(GuildId, FollowTargetType.User, UserId, "user", null,
            FollowEvents.ProjectUpdate);

        await followService.RemoveFollowAsync(GuildId, add.Follow!.Id);

        await using var db = OpenDb(provider);
        Assert.That(await db.ModrinthProjects.AnyAsync(p => p.ProjectId == "a"), Is.True);
    }

    [Test]
    public async Task RemovingAllFollowsKeepsOtherGuildsFollows()
    {
        const ulong otherGuildId = 2;
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        await SeedGuildAsync(provider, otherGuildId);
        var source = new FakeFollowSource();
        source.SetProjects(MakeProject("a", LongAgo));
        var service = BuildService(provider, source);

        await service.AddFollowAsync(GuildId, FollowTargetType.User, UserId, "user", null, FollowEvents.ProjectUpdate);
        await service.AddFollowAsync(otherGuildId, FollowTargetType.User, UserId, "user", null,
            FollowEvents.ProjectUpdate);

        Assert.That(await service.RemoveAllFollowsAsync(GuildId), Is.EqualTo(1));

        await using var db = OpenDb(provider);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await db.ModrinthFollows.Select(f => f.GuildId).ToListAsync(),
                Is.EqualTo(new[] { otherGuildId }));
            Assert.That(await db.ModrinthProjects.AnyAsync(p => p.ProjectId == "a"), Is.True);
        }

        Assert.That(await service.RemoveAllFollowsAsync(otherGuildId), Is.EqualTo(1));

        await using var db2 = OpenDb(provider);
        Assert.That(await db2.ModrinthProjects.CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task RemovingAllSubscriptionsKeepsProjectThatAFollowStillUses()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        var source = new FakeFollowSource();
        source.SetProjects(MakeProject("a", LongAgo));
        var followService = BuildService(provider, source);

        var subscriptionService = new SubscriptionService(provider, NullLogger<SubscriptionService>.Instance);

        await subscriptionService.AddSubscriptionAsync(GuildId, "a", "a-v1", LongAgo, 555, "Project a");
        await subscriptionService.AddSubscriptionAsync(GuildId, "b", "b-v1", LongAgo, 555, "Project b");
        await followService.AddFollowAsync(GuildId, FollowTargetType.User, UserId, "user", null,
            FollowEvents.ProjectUpdate);

        Assert.That(await subscriptionService.RemoveAllSubscriptionsAsync(GuildId), Is.EqualTo(2));

        await using var db = OpenDb(provider);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await db.ModrinthEntries.CountAsync(), Is.EqualTo(0));
            Assert.That(await db.ModrinthProjects.Select(p => p.ProjectId).ToListAsync(), Is.EqualTo(new[] { "a" }));
        }
    }
}
