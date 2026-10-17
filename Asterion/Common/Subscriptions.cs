using System.Text.RegularExpressions;
using Asterion.Database.Models;
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

/// <param name="Result">Result of adding the follow, null if the user was not found</param>
public sealed record FollowUserResult(SearchResult<UserDto> Search, AddFollowResult? Result = null);

/// <summary>
///     Subscribing and following shared by the commands and the manage panel
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

    /// <summary>
    ///     Finds the Modrinth user and follows them
    /// </summary>
    /// <param name="user">Username or ID, see <see cref="ParseUserInput" /></param>
    public static async Task<FollowUserResult> FollowUserAsync(ModrinthService modrinthService,
        IFollowService followService, ulong guildId, string user, ulong channelId, FollowEvents events,
        ulong? newProjectChannelId)
    {
        var search = await modrinthService.FindUser(user);
        if (!search.Success)
            return new FollowUserResult(search);

        var found = search.Payload.User;
        var result = await followService.AddFollowAsync(guildId, FollowTargetType.User, found.Id, found.Username,
            channelId, events, newProjectChannelId);

        return new FollowUserResult(search, result);
    }
    
    /// <summary>
    ///     The project's slug or ID from a slug, ID or link
    /// </summary>
    public static string ParseProjectInput(string input)
    {
        input = input.Trim();

        return UrlParser.TryParseModrinthUrl(ToUrl(input), out var slug) && slug is not null ? slug : input;
    }

    /// <summary>
    ///     The username or ID from a username, ID or user link, null if it is a link to a project
    /// </summary>
    public static string? ParseUserInput(string input)
    {
        input = input.Trim();
        var url = ToUrl(input);

        var userMatch = UserUrlRegex().Match(url);
        if (userMatch.Success)
            return userMatch.Groups[1].Value;

        return UrlParser.TryParseModrinthUrl(url, out _) ? null : input;
    }

    private static string ToUrl(string input)
    {
        return "https://" + SchemeRegex().Replace(input, string.Empty);
    }

    [GeneratedRegex("^https?://", RegexOptions.IgnoreCase)]
    private static partial Regex SchemeRegex();

    // Modrinth.Net has no regex for user links
    [GeneratedRegex(@"^https://(?:www\.)?modrinth\.com/user/([^/?#\s]{1,64})", RegexOptions.IgnoreCase)]
    private static partial Regex UserUrlRegex();
}
