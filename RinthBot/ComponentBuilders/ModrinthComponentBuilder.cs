using Discord;
using Modrinth.RestClient.Models;
using RinthBot.EmbedBuilders;
using Version = Modrinth.RestClient.Models.Version;

namespace RinthBot.ComponentBuilders;

public static class ModrinthComponentBuilder
{
    /// <summary>
    /// Generates link button to view specific version on Modrinth
    /// </summary>
    /// <param name="project"></param>
    /// <param name="version"></param>
    /// <returns></returns>
    public static ButtonBuilder GetVersionUrlButton(Project project, Version version)
    {
        return new ButtonBuilder
        {
            Label = "View on Modrinth",
            Style = ButtonStyle.Link,
            Url = ModrinthEmbedBuilder.GetVersionUrl(project, version)
        };
    }
    
    /// <summary>
    /// Generates link button to view the project on Modrinth
    /// </summary>
    /// <param name="project"></param>
    /// <returns></returns>
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

    public static ButtonBuilder GetUserLinkButton(User user)
    {
        return new ButtonBuilder
        {
            Style = ButtonStyle.Link,
            Url = ModrinthEmbedBuilder.GetUserUrl(user),
            Label = "User on Modrinth"
        };
    }
}