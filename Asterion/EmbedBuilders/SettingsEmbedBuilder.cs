using System.Text;
using Asterion.Database.Models;
using Discord;
using Modrinth.RestClient.Models;
using File = Modrinth.RestClient.Models.File;
using Version = Modrinth.RestClient.Models.Version;

namespace Asterion.EmbedBuilders;

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

    private static Emoji GetEmojiByBool(bool success)
    {
        return success ? Emoji.Parse(":white_check_mark:") : Emoji.Parse(":no_entry_sign:");
    }

    public static EmbedBuilder GetMoreSettingsEmbedBuilder(Guild guild)
    {
        var description = new StringBuilder();

        description.AppendLine(Format.Bold(Format.Underline("Overview of settings")));
        description.AppendLine();
        description.AppendLine($"**1. Check messages for Modrinth links:** {GetEmojiByBool((bool)guild.Settings.CheckMessagesForModrinthLink!)}");
        description.AppendLine(
            $"**2. Show channel selection after subscribe:** {GetEmojiByBool((bool) guild.Settings.ShowChannelSelection!)}");
        description.AppendLine(
            $"**3. Show subscribe button in Modrinth project embeds:** {GetEmojiByBool((bool) guild.Settings.ShowSubscribeButton!)}");
        ;
        
        var embed = new EmbedBuilder()
        {
            Title = "Settings | More",
            Description = description.ToString()
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

        var embed = ModrinthEmbedBuilder.VersionUpdateEmbed(guild.Settings.MessageStyle, dummyProject, dummyVersion);

        return embed;
    }
}