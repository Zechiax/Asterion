using Discord;
using Modrinth.RestClient.Models;
using RinthBot.EmbedBuilders;
using Version = Modrinth.RestClient.Models.Version;

namespace RinthBot.ComponentBuilders;

public static class ModrinthComponentBuilder
{
    public static ButtonBuilder GetVersionUrlButton(Project project, Version version)
    {
        return new ButtonBuilder()
        {
            Label = "View on Modrinth",
            Style = ButtonStyle.Link,
            Url = ModrinthEmbedBuilder.GetVersionUrl(project, version)
        };
    }
    
    public static ButtonBuilder GetProjectLinkButton(Project project)
    {
        var linkBtn = new ButtonBuilder()
        {
            Style = ButtonStyle.Link,
            Url = ModrinthEmbedBuilder.GetProjectUrl(project),
            Label = "Project's site"
        };

        return linkBtn;
    }
}