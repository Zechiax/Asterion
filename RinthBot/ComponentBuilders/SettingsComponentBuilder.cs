using Discord;

namespace RinthBot.ComponentBuilders;

public static class SettingsComponentBuilder
{
    public static ComponentBuilder GetIntroButtons()
    {
        var components = 
            new ComponentBuilder()
                .WithButton(GetViewButton())
                .WithButton(GetChangeSettingsButton());

        return components;
    }

    public static ButtonBuilder GetViewButton()
    {
        return new ButtonBuilder
        {
            Label = "Notification style",
            CustomId = "settings-module-notification-style",
            Style = ButtonStyle.Secondary
        };
    }

    public static ButtonBuilder GetChangeSettingsButton()
    {
        return new ButtonBuilder
        {
            Label = "Bot settings",
            CustomId = "settings-module-global-bot",
            Style = ButtonStyle.Secondary
        };
    }
}