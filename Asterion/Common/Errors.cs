using Asterion.Services.Modrinth;
using Discord;

namespace Asterion.Common;

/// <summary>
///     Errors shown to users, with the arguments their messages need
/// </summary>
public static class Errors
{
    public static readonly StatusCard Internal = new(StatusKind.Error, "error-internal");
    public static readonly StatusCard Unknown = new(StatusKind.Error, "error-unknown");
    public static readonly StatusCard CommandFailed = new(StatusKind.Error, "error-command-failed");
    public static readonly StatusCard InvalidInput = new(StatusKind.Error, "error-invalid-input");
    public static readonly StatusCard MenuOutdated = new(StatusKind.Error, "error-menu-outdated");

    public static readonly StatusCard NoMoreResults = new(StatusKind.Error, "error-no-more-results");
    public static readonly StatusCard MessageEmpty = new(StatusKind.Error, "error-message-empty");
    public static readonly StatusCard InputIsProject = new(StatusKind.Error, "error-input-is-project");

    public static readonly StatusCard NoStatsData = new(StatusKind.Error, "error-no-stats-data");

    public static readonly StatusCard NotAllowed = new(StatusKind.Error, "error-not-allowed");
    public static readonly StatusCard GuildOnly = new(StatusKind.Error, "error-guild-only");
    public static readonly StatusCard ManagePermission = new(StatusKind.Error, "error-manage-permission");
    public static readonly StatusCard NotPanelOwner = new(StatusKind.Error, "error-not-panel-owner");
    public static readonly StatusCard InvalidChannel = new(StatusKind.Error, "error-invalid-channel");
    public static readonly StatusCard ChannelNotText = new(StatusKind.Error, "error-channel-not-text");

    public static StatusCard BotCannotAccessChannel(string channelMention)
    {
        return new StatusCard(StatusKind.Error, "error-bot-cannot-access-channel", new { channel = channelMention });
    }

    public static StatusCard ProjectNotFound(string query)
    {
        return new StatusCard(StatusKind.Error, "error-project-not-found", new { query = Format.Sanitize(query) });
    }

    public static StatusCard ChangelogLengthInvalid(int min, int max)
    {
        return new StatusCard(StatusKind.Error, "error-changelog-length-invalid", new { min, max });
    }

    /// <param name="wait">Roughly how long until there is enough data</param>
    public static StatusCard NotEnoughData(TimeSpan wait)
    {
        var args = wait < TimeSpan.FromDays(2)
            ? new { unit = "hours", amount = (int)Math.Ceiling(wait.TotalHours) }
            : new { unit = "days", amount = (int)Math.Ceiling(wait.TotalDays) };

        return new StatusCard(StatusKind.Error, "error-not-enough-data", args);
    }

    public static StatusCard DangerNotConfirmed(string word)
    {
        return new StatusCard(StatusKind.Error, "error-danger-not-confirmed", new { word });
    }

    /// <summary>
    ///     Why a search was unsuccessful
    /// </summary>
    public static StatusCard FromSearch<T>(SearchResult<T> result)
    {
        var key = result.SearchStatus switch
        {
            SearchStatus.ApiDown => "error-modrinth-api-unavailable",
            SearchStatus.NoResult => "error-search-no-result",
            SearchStatus.UnknownError => "error-unknown",
            _ => throw new ArgumentException("SearchResult was successful, but was expected to be an error",
                nameof(result))
        };

        return new StatusCard(StatusKind.Error, key, new { query = Format.Sanitize(result.Query) },
            "error-search-unsuccessful");
    }
}

/// <summary>
///     Warnings shown to users, the action went through but something needs their attention
/// </summary>
public static class Warnings
{
    public static readonly StatusCard AlreadySubscribed = new(StatusKind.Warning, "warning-already-subscribed");
    public static readonly StatusCard AlreadyUnsubscribed = new(StatusKind.Warning, "warning-already-unsubscribed");
    public static readonly StatusCard NotFollowing = new(StatusKind.Warning, "warning-not-following");

    public static StatusCard BotCannotSend(string channelMention)
    {
        return new StatusCard(StatusKind.Warning, "warning-bot-cannot-send", new { channel = channelMention });
    }
}
