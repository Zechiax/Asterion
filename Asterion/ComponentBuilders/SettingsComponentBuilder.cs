using Asterion.Database.Models;
using Discord;

namespace Asterion.ComponentBuilders;

public static class SettingsComponentBuilder
{
    // Should be left as it is, as it would break backwards compatibility
    public const string NotificationButtonId = "settings-module-notification-style:*";
    public const string MoreButtonId = "settings-module-more:*";
    public const string MainScreenButtonId = "settings-main:*";
    public const string ChangeMessageStyleSelectionId = "change-message-style:*";
    public const string ChangeChangelogStyleSelectionId = "change-changelog-style:*";

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

    public static ButtonBuilder GetBackButton(string userId)
    {
        return new ButtonBuilder
        {
            Label = "Back",
            CustomId = MainScreenButtonId.Replace("*", userId),
            Emote = Emoji.Parse(":back:"),
            Style = ButtonStyle.Secondary
        };
    }

    public static ComponentBuilder GetMoreSettingsComponents(string userId, GuildSettings guildSettings)
    {
        return new ComponentBuilder()
            .WithRows(new[]
            {
                // First row
                new ActionRowBuilder()
                    .WithButton(new ButtonBuilder
                    {
                        Label = "1. Show subscribe button",
                        CustomId = $"settings-show-subscribe-button:{userId};{guildSettings.ShowSubscribeButton}",
                        Style = ButtonStyle.Primary
                    }),
                // Second row
                new ActionRowBuilder()
                    .WithButton(GetBackButton(userId))
            });
    }

    public static ComponentBuilder GetMessageStyleSelectionComponents(Guild guild, string userId)
    {
        return new ComponentBuilder()
            .WithSelectMenu(GetMessageStyleSelection(guild, userId))
            .WithSelectMenu(GetChangelogStyleSelection(guild, userId))
            .WithButton(GetBackButton(userId), 1);
    }

    public static SelectMenuBuilder GetMessageStyleSelection(Guild guild, string userId)
    {
        var menu = new SelectMenuBuilder
        {
            MaxValues = 1,
            MinValues = 1,
            CustomId = ChangeMessageStyleSelectionId.Replace("*", userId),
            Placeholder = "Select a message style"
        };

        var options = new List<SelectMenuOptionBuilder>();
        foreach (var style in Enum.GetValues(typeof(MessageStyle)).Cast<MessageStyle>())
            options.Add(new SelectMenuOptionBuilder
            {
                Label = Enum.GetName(typeof(MessageStyle), style),
                Value = style.ToString(),
                IsDefault = style == guild.GuildSettings.MessageStyle
            });

        menu.Options = options;

        return menu;
    }

    public static SelectMenuBuilder GetChangelogStyleSelection(Guild guild, string userId)
    {
        var menu = new SelectMenuBuilder
        {
            MaxValues = 1,
            MinValues = 1,
            CustomId = ChangeChangelogStyleSelectionId.Replace("*", userId),
            Placeholder = "Select a changelog style"
        };

        var options = new List<SelectMenuOptionBuilder>();
        foreach (var style in Enum.GetValues(typeof(ChangelogStyle)).Cast<ChangelogStyle>())
            options.Add(new SelectMenuOptionBuilder
            {
                Label = Enum.GetName(typeof(ChangelogStyle), style),
                Value = style.ToString(),
                IsDefault = style == guild.GuildSettings.ChangelogStyle
            });

        menu.Options = options;

        return menu;
    }
}