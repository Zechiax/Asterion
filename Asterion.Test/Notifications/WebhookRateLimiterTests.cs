using Asterion.Services.Notifications.Delivery;

namespace Asterion.Test.Notifications;

[TestFixture]
public class WebhookRateLimiterTests
{
    [Test]
    public void AllowsSendWhenUnderLimit()
    {
        var limiter = new WebhookRateLimiter(maxPerWindow: 2, window: TimeSpan.FromSeconds(60));
        var now = DateTime.UtcNow;

        Assert.That(limiter.TryAcquire("https://example.com/webhook", now), Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void BlocksSendOnceLimitReachedWithinWindow()
    {
        var limiter = new WebhookRateLimiter(maxPerWindow: 2, window: TimeSpan.FromSeconds(60));
        var now = DateTime.UtcNow;
        const string url = "https://example.com/webhook";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(limiter.TryAcquire(url, now), Is.EqualTo(TimeSpan.Zero));
            Assert.That(limiter.TryAcquire(url, now.AddSeconds(1)), Is.EqualTo(TimeSpan.Zero));
        }

        var wait = limiter.TryAcquire(url, now.AddSeconds(2));

        Assert.That(wait, Is.GreaterThan(TimeSpan.Zero));
    }

    [Test]
    public void ResetsAfterWindowElapses()
    {
        var limiter = new WebhookRateLimiter(maxPerWindow: 1, window: TimeSpan.FromSeconds(60));
        var now = DateTime.UtcNow;
        const string url = "https://example.com/webhook";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(limiter.TryAcquire(url, now), Is.EqualTo(TimeSpan.Zero));
            Assert.That(limiter.TryAcquire(url, now.AddSeconds(30)), Is.GreaterThan(TimeSpan.Zero));

            // Window has fully elapsed - a new window starts and allows a send again
            Assert.That(limiter.TryAcquire(url, now.AddSeconds(61)), Is.EqualTo(TimeSpan.Zero));
        }
    }

    [Test]
    public void TracksEachWebhookUrlIndependently()
    {
        var limiter = new WebhookRateLimiter(maxPerWindow: 1, window: TimeSpan.FromSeconds(60));
        var now = DateTime.UtcNow;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(limiter.TryAcquire("https://example.com/webhook-a", now), Is.EqualTo(TimeSpan.Zero));
            Assert.That(limiter.TryAcquire("https://example.com/webhook-b", now), Is.EqualTo(TimeSpan.Zero));
        }
    }
}
