using Discord.WebSocket;

namespace Asterion.Services.Notifications.Delivery;

/// <summary>
///     Sender with DiscordSocketClient - to send messages directly to channels.
/// </summary>
public class BotSender : INotificationSender
{
    private readonly DiscordSocketClient _client;

    public BotSender(DiscordSocketClient client)
    {
        _client = client;
    }

    public bool CanHandle(DeliveryTarget target) => target is BotDeliveryTarget;

    public async Task SendAsync(DeliveryTarget target, NotificationContent content, CancellationToken ct)
    {
        var botTarget = (BotDeliveryTarget)target;

        var channel = _client.GetGuild(botTarget.GuildId)?.GetTextChannel(botTarget.ChannelId);
        if (channel is null)
            throw new NotificationSendException(
                $"Channel {botTarget.ChannelId} not found in guild {botTarget.GuildId}");

        try
        {
            await channel.SendMessageAsync(content.PingText, embed: content.Embed, components: content.Components);
        }
        catch (Exception ex)
        {
            throw new NotificationSendException($"Failed to send message to channel {botTarget.ChannelId}", ex);
        }
    }
}
