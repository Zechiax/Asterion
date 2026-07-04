using Discord;

namespace Asterion.Services.Notifications.Delivery;

public sealed record NotificationContent(string PingText, Embed Embed, MessageComponent Components);
