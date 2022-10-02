using Discord;
using RinthBot.Database.Models;

namespace RinthBot.EmbedBuilders;

public static class SettingsEmbedBuilder
{
    public static EmbedBuilder GetIntroEmbedBuilder()
    {
        var embed = new EmbedBuilder
        {
            Title = "Settings",
            Description = "Hi!" +
                          "\n\n" +
                          "Here you'll find all the settings to setup the bot for your server needs. Use the buttons to navigate sections and changing the settings."
        };

        return embed;
    }

    public static EmbedBuilder GetMoreSettingsEmbedBuilder(Guild guild)
    {
        var embed = new EmbedBuilder()
        {
            Title = "Settings | More",
            Description = "TBD overview of active settings"
        };

        return embed;
    }
}