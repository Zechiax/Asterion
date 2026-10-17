using System.Text.RegularExpressions;
using Asterion.Interfaces;
using Asterion.Services.Modrinth;
using Modrinth.Helpers;
using Modrinth.Models;

namespace Asterion.Common;

public enum SubscribeStatus
{
    Subscribed,
    AlreadySubscribed,
    ProjectNotFound,
    Failed
}

/// <param name="Project">The project, null if it was not found</param>
public sealed record SubscribeResult(SubscribeStatus Status, Project? Project = null);

/// <summary>
///     Subscribing shared by <c>/subscribe</c> and the manage panel
/// </summary>
public static partial class Subscriptions
{
    /// <summary>
    ///     Subscribes the guild to the project, updates are sent to the channel
    /// </summary>
    public static async Task<SubscribeResult> SubscribeAsync(ModrinthService modrinthService,
        IDataService dataService, ulong guildId, string slugOrId, ulong channelId)
    {
        var project = await modrinthService.GetProject(slugOrId);
        if (project is null)
            return new SubscribeResult(SubscribeStatus.ProjectNotFound);

        if (await dataService.IsGuildSubscribedToProjectAsync(guildId, project.Id))
            return new SubscribeResult(SubscribeStatus.AlreadySubscribed, project);

        var lastVersion = (await modrinthService.GetVersionListAsync(project.Id))?
            .OrderByDescending(x => x.DatePublished)
            .FirstOrDefault();
        if (lastVersion is null)
            return new SubscribeResult(SubscribeStatus.Failed, project);

        var added = await dataService.AddModrinthProjectToGuildAsync(guildId, project.Id, lastVersion.Id,
            project.Updated, channelId, project.Title);

        return new SubscribeResult(added ? SubscribeStatus.Subscribed : SubscribeStatus.Failed, project);
    }
    
    public static string ParseModrinthInput(string input)
    {
        input = input.Trim();
        
        var url = "https://" + SchemeRegex().Replace(input, string.Empty);
        if (UrlParser.TryParseModrinthUrl(url, out var slug) && slug is not null)
            return slug;

        var userMatch = UserUrlRegex().Match(url);
        return userMatch.Success ? userMatch.Groups[1].Value : input;
    }

    [GeneratedRegex("^https?://", RegexOptions.IgnoreCase)]
    private static partial Regex SchemeRegex();

    // Modrinth.Net has no regex for user links
    [GeneratedRegex(@"^https://(?:www\.)?modrinth\.com/user/([^/?#\s]{1,64})", RegexOptions.IgnoreCase)]
    private static partial Regex UserUrlRegex();
}
