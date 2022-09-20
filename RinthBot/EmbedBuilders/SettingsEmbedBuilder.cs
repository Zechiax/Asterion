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
            Description = "*placeholder text*"
        };

        return embed;
    }
}