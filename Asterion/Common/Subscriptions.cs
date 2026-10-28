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

/// <param name="Found">The subscription or follow, null if there is none</param>
/// <param name="Error">Set if Modrinth could not be asked, then <paramref name="Found" /> is null</param>
public sealed record LookupResult<T>(T? Found, StatusCard? Error = null) where T : class;

/// <summary>
///     Subscribing and following shared by the commands and the manage panel
/// </summary>
public static partial class Subscriptions
{
    /// <summary>
    ///     Subscribes the guild to the project, updates are sent to the channel
    /// </summary>
    public static async Task<SubscribeResult> SubscribeAsync(ModrinthService modrinthService,
        ISubscriptionService subscriptionService, ulong guildId, string slugOrId, ulong channelId)
    {
        var project = await modrinthService.GetProject(slugOrId);
        if (project is null)
            return new SubscribeResult(SubscribeStatus.ProjectNotFound);

        if (await subscriptionService.IsSubscribedAsync(guildId, project.Id))
            return new SubscribeResult(SubscribeStatus.AlreadySubscribed, project);

        var lastVersion = (await modrinthService.GetVersionListAsync(project.Id))?
            .OrderByDescending(x => x.DatePublished)
            .FirstOrDefault();
        if (lastVersion is null)
            return new SubscribeResult(SubscribeStatus.Failed, project);

        var added = await subscriptionService.AddSubscriptionAsync(guildId, project.Id, lastVersion.Id,
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
    ///     The guild's subscription of the project, null if the guild is not subscribed to it
    /// </summary>
    /// <param name="input">Slug, ID or link, see <see cref="ParseProjectInput" /></param>
    public static async Task<LookupResult<ModrinthEntry>> FindSubscriptionAsync(ModrinthService modrinthService,
        ISubscriptionService subscriptionService, ulong guildId, string input)
    {
        var entries = await subscriptionService.GetSubscriptionsAsync(guildId);
        if (entries.Count == 0)
            return new LookupResult<ModrinthEntry>(null);

        var query = ParseProjectInput(input);
        var entry = entries.FirstOrDefault(e => e.ProjectId == query);
        if (entry is not null)
            return new LookupResult<ModrinthEntry>(entry);

        // Slugs are not stored, Modrinth knows the ID behind them
        var search = await modrinthService.FindProject(query);
        return search.SearchStatus switch
        {
            // A search hit is a different project than the one typed
            SearchStatus.FoundById => new LookupResult<ModrinthEntry>(
                entries.FirstOrDefault(e => e.ProjectId == search.Payload.Project.Id)),
            SearchStatus.FoundBySearch or SearchStatus.NoResult => new LookupResult<ModrinthEntry>(null),
            _ => new LookupResult<ModrinthEntry>(null, Errors.FromSearch(search))
        };
    }

    /// <summary>
    ///     The follow with the user ID or stored username, null if none matches
    /// </summary>
    /// <param name="follows">Follows of users</param>
    /// <param name="user">Username or ID, see <see cref="ParseUserInput" /></param>
    public static ModrinthFollow? MatchFollow(IList<ModrinthFollow> follows, string user)
    {
        return follows.FirstOrDefault(f => f.TargetId == user)
               ?? follows.FirstOrDefault(f => string.Equals(f.TargetName, user, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     The guild's follow of the user, null if the guild does not follow them
    /// </summary>
    /// <param name="input">Username, ID or link, see <see cref="ParseUserInput" /></param>
    public static async Task<LookupResult<ModrinthFollow>> FindFollowAsync(ModrinthService modrinthService,
        IFollowService followService, ulong guildId, string input)
    {
        var user = ParseUserInput(input);
        if (user is null)
            return new LookupResult<ModrinthFollow>(null);

        var follows = (await followService.GetFollowsAsync(guildId))
            .Where(f => f.TargetType == FollowTargetType.User)
            .ToList();
        if (follows.Count == 0)
            return new LookupResult<ModrinthFollow>(null);

        var follow = MatchFollow(follows, user);
        if (follow is not null)
            return new LookupResult<ModrinthFollow>(follow);

        // The stored name is old if the user was renamed, Modrinth knows the ID behind the new one
        var search = await modrinthService.FindUser(user);
        if (search.Success)
            return new LookupResult<ModrinthFollow>(follows.FirstOrDefault(f => f.TargetId == search.Payload.User.Id));

        return search.SearchStatus == SearchStatus.NoResult
            ? new LookupResult<ModrinthFollow>(null)
            : new LookupResult<ModrinthFollow>(null, Errors.FromSearch(search));
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
