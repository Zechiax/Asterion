using System.ComponentModel;
using Discord;
using Modrinth.RestClient.Extensions;
using Modrinth.RestClient.Models;
using Asterion.EmbedBuilders;
using Version = Modrinth.RestClient.Models.Version;

namespace Asterion.ComponentBuilders;

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
    
    public static ButtonBuilder GetSubscribeButtons(ulong userId, string projectId,
        bool subEnabled = true)
    {
        var button = new ButtonBuilder(
            label: subEnabled ? "Subscribe" : "Unsubscribe",
            // Write unsub when the subEnabled is false
            customId: $"{(subEnabled ? null : "un")}sub-project:{userId};{projectId}",
            style: subEnabled ? ButtonStyle.Success : ButtonStyle.Danger,
            emote: subEnabled ? Emoji.Parse(":bell:") : Emoji.Parse(":no_bell:"));
        

        return button;
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

    public static ButtonBuilder ViewMoreSearchResults(SearchResponse? results, string query, int maxResults = 10)
    {
        var button = new ButtonBuilder(
            customId: $"more-results:|{query}|".Replace(' ', '_'),
            style: ButtonStyle.Primary,
            label: $"View more results ({(results is not null ? Math.Min(results.Hits.Length, maxResults) : '0')})",
            emote: Emoji.Parse(":mag_right:"),
            isDisabled: results is null || results.Hits.Length <= 1
        );

        return button;
    }

    public static ComponentBuilder GetResultSearchButton(SearchResult[] projects)
    {
        var components = new ComponentBuilder();

        var rowCounter = 0;
        for (var i = 0; i < projects.Length; i++)
        {
            var p = projects[i];
            components.WithButton(new ButtonBuilder(label: (i + 1).ToString(), customId: $"view-project-from-search:{p.ProjectId}"), rowCounter);

            if (i % 5 == 0 && i != 0)
            {
                rowCounter++;
            }
        }

        return components;
    }
}