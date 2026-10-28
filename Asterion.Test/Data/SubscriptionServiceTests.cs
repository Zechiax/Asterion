using Asterion.Interfaces;
using Asterion.Services.Subscriptions;
using Asterion.Test.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using static Asterion.Test.Follows.FollowTestData;
using static Asterion.Test.Notifications.InMemoryDb;

namespace Asterion.Test.Data;

[TestFixture]
public class SubscriptionServiceTests
{
    [Test]
    public async Task SubscribingTwiceKeepsOneEntry()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedGuildAsync(provider);
        var service = new SubscriptionService(provider, NullLogger<SubscriptionService>.Instance);

        var first = await service.AddSubscriptionAsync(GuildId, "a", "a-v1", DateTime.UtcNow, 555, "Project a");
        var second = await service.AddSubscriptionAsync(GuildId, "a", "a-v1", DateTime.UtcNow, 555, "Project a");

        await using var db = OpenDb(provider);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.EqualTo(AddSubscriptionResult.Added));
            Assert.That(second, Is.EqualTo(AddSubscriptionResult.AlreadySubscribed));
            Assert.That(await db.ModrinthEntries.CountAsync(), Is.EqualTo(1));
        }
    }
}
