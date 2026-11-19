using Asterion.Database.Models;
using Asterion.Services.Guilds;
using Asterion.Services.Subscriptions;
using Asterion.Test.Fakes;
using Asterion.Test.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using static Asterion.Test.Follows.FollowTestData;
using static Asterion.Test.Notifications.InMemoryDb;

namespace Asterion.Test.Data;

[TestFixture]
public class GuildServiceTests
{
    private static readonly DateTime LongAgo = DateTime.UtcNow.AddYears(-1);

    [Test]
    public async Task RemovingGuildDeletesItsDataAndOnlyItsProjects()
    {
        const ulong otherGuildId = 2;
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        await SeedGuildAsync(provider, otherGuildId);
        var source = new FakeFollowSource();
        source.SetProjects(MakeProject("c", LongAgo));
        var followService = BuildService(provider, source);
        var subscriptionService = new SubscriptionService(provider, NullLogger<SubscriptionService>.Instance);
        var guildService = new GuildService(provider, NullLogger<GuildService>.Instance);

        await subscriptionService.AddSubscriptionAsync(GuildId, "a", "a-v1", LongAgo, 555, "Project a");
        await subscriptionService.AddSubscriptionAsync(GuildId, "b", "b-v1", LongAgo, 555, "Project b");
        await subscriptionService.AddSubscriptionAsync(otherGuildId, "a", "a-v1", LongAgo, 555, "Project a");
        await followService.AddFollowAsync(GuildId, FollowTargetType.User, UserId, "user", null,
            FollowEvents.ProjectUpdate);

        Assert.That(await guildService.RemoveGuildAsync(GuildId), Is.True);

        await using var db = OpenDb(provider);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await db.Guilds.Select(g => g.GuildId).ToListAsync(), Is.EqualTo(new[] { otherGuildId }));
            Assert.That(await db.Arrays.CountAsync(), Is.EqualTo(1));
            Assert.That(await db.GuildSettings.CountAsync(), Is.EqualTo(1));
            Assert.That(await db.ModrinthEntries.CountAsync(), Is.EqualTo(1));
            Assert.That(await db.ModrinthFollows.CountAsync(), Is.EqualTo(0));
            Assert.That(await db.ModrinthProjects.Select(p => p.ProjectId).ToListAsync(), Is.EqualTo(new[] { "a" }));
        }
    }

    [Test]
    public async Task CountAndResetFilterOverridesOnlyAffectsTargetGuild()
    {
        const ulong otherGuildId = 2;
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        await SeedGuildAsync(provider, otherGuildId);

        await using (var db = OpenDb(provider))
        {
            db.ModrinthEntries.AddRange(
                new ModrinthEntry { GuildId = GuildId, ProjectId = "a", ReleaseFilter = ReleaseType.Release },
                new ModrinthEntry { GuildId = GuildId, ProjectId = "b" },
                new ModrinthEntry { GuildId = otherGuildId, ProjectId = "a", LoaderFilter = ["fabric"] });
            db.ModrinthFollows.Add(new ModrinthFollow
                { GuildId = GuildId, TargetId = UserId, TargetName = "u", LoaderFilter = [] });
            await db.SaveChangesAsync();
        }

        var guildService = new GuildService(provider, NullLogger<GuildService>.Instance);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await guildService.CountFilterOverridesAsync(GuildId), Is.EqualTo(2));
            Assert.That(await guildService.ResetFilterOverridesAsync(GuildId), Is.EqualTo(2));
            Assert.That(await guildService.CountFilterOverridesAsync(GuildId), Is.EqualTo(0));
            Assert.That(await guildService.CountFilterOverridesAsync(otherGuildId), Is.EqualTo(1));
        }
    }
}
