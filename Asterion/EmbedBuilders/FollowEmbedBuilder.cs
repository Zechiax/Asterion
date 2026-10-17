using System.Globalization;
using Asterion.Database.Models;
using Asterion.Interfaces;
using Discord;
using Humanizer;

namespace Asterion.EmbedBuilders;

public static class FollowEmbedBuilder
{
    public static string FormatEvents(FollowEvents events, ILocalizationService loc, CultureInfo? culture)
    {
        var names = new List<string>();

        if (events.HasFlag(FollowEvents.ProjectUpdate))
            names.Add(loc.Get("follow-event-project-update", culture));
        if (events.HasFlag(FollowEvents.NewProject))
            names.Add(loc.Get("follow-event-new-project", culture));

        return names.Count == 0 ? loc.Get("follow-event-none", culture) : string.Join(", ", names);
    }

    public static string DescribeAddResult(AddFollowResult result, string targetName, string channelMention,
        ILocalizationService loc, CultureInfo? culture)
    {
        targetName = Format.Sanitize(targetName);

        return result.Status switch
        {
            AddFollowStatus.Added when result.Sync is { Success: false } sync =>
                loc.Get("follow-added-sync-failed", culture, new { name = targetName, error = sync.Error ?? "" }),
            AddFollowStatus.Added => loc.Get("follow-added", culture,
                new { name = targetName, count = result.Sync?.TrackedCount ?? 0, channel = channelMention }),
            AddFollowStatus.AlreadyFollowing => loc.Get("follow-already-following", culture, new { name = targetName }),
            AddFollowStatus.LimitReached => loc.Get("follow-limit-reached", culture,
                new { max = IFollowService.MaxFollowsPerGuild }),
            _ => loc.Get("error-internal", culture)
        };
    }

    public static string FormatTargetType(FollowTargetType targetType, ILocalizationService loc, CultureInfo? culture)
    {
        return targetType switch
        {
            FollowTargetType.Organization => loc.Get("follow-target-organization", culture),
            _ => loc.Get("follow-target-user", culture)
        };
    }

    public static string FormatListEntry(ModrinthFollow follow, ILocalizationService loc, CultureInfo? culture)
    {
        return $"**{Format.Sanitize(follow.TargetName)}**\n" + loc.Get("follow-list-entry", culture, new
        {
            type = FormatTargetType(follow.TargetType, loc, culture),
            events = FormatEvents(follow.Events, loc, culture),
            channel = follow.CustomUpdateChannel.HasValue
                ? MentionUtils.MentionChannel(follow.CustomUpdateChannel.Value)
                : loc.Get("settings-none", culture),
            projects = follow.Projects.Count,
            ignored = follow.Projects.Count(fp => fp.Ignored)
        });
    }
}
