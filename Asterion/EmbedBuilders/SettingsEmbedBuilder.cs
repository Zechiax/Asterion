using Asterion.Database.Models;
using Discord;
using Modrinth.Models;
using File = Modrinth.Models.File;
using Version = Modrinth.Models.Version;

namespace Asterion.EmbedBuilders;

public static class SettingsEmbedBuilder
{
    public static EmbedBuilder GetViewSettingsEmbed(Guild guild)
    {
        var dummyProject = new Project
        {
            Slug = "dummy-project",
            Id = "12345678",
            IconUrl = "https://avatars.githubusercontent.com/u/67560307",
            Description = "This is project's description",
            Title = "Dummy project",
            Categories = new[] {"dummy"}
        };

        var dummyVersion = new Version
        {
            Changelog =
                "This is a changelog \n\n with multiple lines\n and a Markdown link [here](https://modrinth.com/)\n" +
                "and a style test: **bold**, *italic*, ~~strikethrough~~, `code`, __underline__\n" +
                "So that you can see how it looks like in the embed. 🐱\n\n" +
                "Although most of the time, the changelog will be a list of changes, like this one:\n" +
                "- Added a new feature\n" +
                "- Fixed a bug\n" +
                "- Changed something else",
            Id = "12456789",
            DatePublished = DateTime.Now,
            GameVersions = new[] {"1.19.2"},
            Files = new[]
                {new File {Size = 1024, Url = "https://modrinth.com/", FileName = "non-existent-file.not.jar"}},
            Loaders = new[] {"Fabric"},
            Name = "This version's name",
            VersionNumber = "Version-3.5"
        };

        var embed = ModrinthEmbedBuilder.VersionUpdateEmbed(guild.GuildSettings, dummyProject, dummyVersion);

        return embed;
    }
}