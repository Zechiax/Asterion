namespace Asterion.Services.Notifications.Delivery;

public abstract record DeliveryTarget;

public sealed record BotDeliveryTarget(ulong GuildId, ulong ChannelId) : DeliveryTarget;

public sealed record WebhookDeliveryTarget(string WebhookUrl) : DeliveryTarget;
