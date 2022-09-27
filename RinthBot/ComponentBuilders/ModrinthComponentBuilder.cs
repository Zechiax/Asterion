using Discord;
using Modrinth.RestClient.Extensions;
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
            Url = project.GetVersionUrl(version)
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
            Url = project.Url,
            Label = "Project's site"
        };

        return linkBtn;
    }

    public static ButtonBuilder GetUserLinkButton(User user)
    {
        return new ButtonBuilder
        {
            Style = ButtonStyle.Link,
            Url = user.Url,
            Label = "User on Modrinth"
        };
    }
    
    public static ComponentBuilder GetSubscribeButtons(ulong userId, string projectId,
        bool subEnabled = true)
    {
        var buttons = new ComponentBuilder()
            .WithButton(
                subEnabled ? "Subscribe" : "Unsubscribe",
                // Write unsub when the subEnabled is false
                customId: $"{(subEnabled ? null : "un")}sub-project:{userId};{projectId}",
                style: subEnabled ? ButtonStyle.Success : ButtonStyle.Danger,
                emote: subEnabled ? Emoji.Parse(":bell:") : Emoji.Parse(":no_bell:"));

        return buttons;
    }

    /// <summary>
    /// Creates button to view user details from a project view
    /// </summary>
    /// <param name="discordUserId"></param>
    /// <param name="modrinthUserId">Id of the Modrinth user to show, if null, button will be disabled</param>
    /// <param name="projectId"></param>
    /// <returns></returns>
    public static ButtonBuilder GetUserToViewButton(ulong discordUserId, string? modrinthUserId, string projectId)
    {
        var button = new ButtonBuilder(
            customId: $"show-user:{discordUserId};{modrinthUserId};{projectId}",
            style: ButtonStyle.Primary,
            label: "See owner",
            isDisabled: string.IsNullOrEmpty(modrinthUserId));

        return button;
    }
    
    /// <summary>
    /// Creates button to view user details from a project view
    /// </summary>
    /// <param name="discordUserId"></param>
    /// <param name="modrinthUserId">Id of the Modrinth user to show, if null, button will be disabled</param>
    /// <param name="projectId"></param>
    /// <returns></returns>
    public static ButtonBuilder BackToProjectButton(ulong discordUserId, string projectId)
    {
        var button = new ButtonBuilder(
            customId: $"back-project:{discordUserId};{projectId}",
            style: ButtonStyle.Primary,
            label: "To project view",
            emote: Emoji.Parse(":back:"));

        return button;
    }
}