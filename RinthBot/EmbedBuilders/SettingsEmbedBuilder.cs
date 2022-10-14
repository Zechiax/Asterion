using Discord;
using Modrinth.RestClient.Models;
using RinthBot.Database.Models;
using File = Modrinth.RestClient.Models.File;
using Version = Modrinth.RestClient.Models.Version;

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

    public static EmbedBuilder GetViewSettingsEmbed(Guild guild)
    {
        var dummyProject = new Project()
        {
            Slug = "dummy-project",
            Id = "12345678",
            IconUrl = "https://avatars.githubusercontent.com/u/67560307",
            Description = "This is project's description",
            Title = "Dummy project",
            Categories = new [] {"dummy"},
            
        };

        var dummyVersion = new Version()
        {
            Changelog = "This would be the new version's changelog",
            Id = "12456789",
            DatePublished = DateTime.Now,
            GameVersions = new[] {"1.19.2"},
            Files = new[] {new File() {Size = 1024, Url = "https://modrinth.com/", FileName = "non-existent-file.not.jar"} },
            Loaders = new[] {"Fabric"},
            Name = "This version's name",
            VersionNumber = "Version-3.5"
        };

        var embed = ModrinthEmbedBuilder.VersionUpdateEmbed(guild.GuildSettings.MessageStyle, dummyProject, dummyVersion);

        return embed;
    }
}