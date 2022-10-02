using Discord;
using RinthBot.Database.Models;

namespace RinthBot.ComponentBuilders;

public static class SettingsComponentBuilder
{
    // Should be left as it is, as it would break backwards compatibility
    public const string NotificationButtonId = "settings-module-notification-style:*";
    public const string MoreButtonId = "settings-module-more:*";
    public static ComponentBuilder GetIntroButtons(string userId)
    {
        var components = 
            new ComponentBuilder()
                .WithButton(GetViewButton(userId))
                .WithButton(GetChangeSettingsButton(userId));

        return components;
    }

    public static ButtonBuilder GetViewButton(string userId)
    {
        return new ButtonBuilder
        {
            Label = "Notification style",
            CustomId = NotificationButtonId.Replace("*", userId),
            Style = ButtonStyle.Secondary
        };
    }

    public static ButtonBuilder GetChangeSettingsButton(string userId)
    {
        return new ButtonBuilder
        {
            Label = "Bot settings",
            CustomId = MoreButtonId.Replace("*", userId),
            Style = ButtonStyle.Secondary
        };
    }

    public static ComponentBuilder GetMoreSettingsComponents(string userId, GuildSettings guildSettings)
    {
        return new ComponentBuilder()
            .WithButton(new ButtonBuilder()
            {
                Label = "[Experimental] Scan messages for Modrinth link",
                CustomId = $"settings-message-scan:{userId};{guildSettings.CheckMessagesForModrinthLink}",
                Emote = Emoji.Parse(":warning:"),
                Style = guildSettings.CheckMessagesForModrinthLink == true ? ButtonStyle.Danger : ButtonStyle.Success
            });
    }
}