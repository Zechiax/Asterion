using Discord.Webhook;

namespace Asterion.Services.Notifications.Delivery;

/// <summary>
///     Webhook sender
/// </summary>
public class WebhookSender : INotificationSender
{
    private readonly WebhookRateLimiter _rateLimiter;

    public WebhookSender(WebhookRateLimiter rateLimiter)
    {
        _rateLimiter = rateLimiter;
    }

    public bool CanHandle(DeliveryTarget target) => target is WebhookDeliveryTarget;

    public async Task SendAsync(DeliveryTarget target, NotificationContent content, CancellationToken ct)
    {
        var webhookTarget = (WebhookDeliveryTarget)target;

        var wait = _rateLimiter.TryAcquire(webhookTarget.WebhookUrl, DateTime.UtcNow);
        if (wait > TimeSpan.Zero)
            throw new RateLimitedException(wait);

        using var client = new DiscordWebhookClient(webhookTarget.WebhookUrl);

        try
        {
            await client.SendMessageAsync(content.PingText, embeds: new[] { content.Embed },
                components: content.Components);
        }
        catch (Exception ex)
        {
            throw new NotificationSendException($"Failed to send webhook message to {webhookTarget.WebhookUrl}", ex);
        }
    }
}
