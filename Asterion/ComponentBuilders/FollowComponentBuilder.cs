using System.Globalization;
using Asterion.Database.Models;
using Asterion.EmbedBuilders;
using Asterion.Interfaces;
using Discord;
using Modrinth.Models;

namespace Asterion.ComponentBuilders;

public static class FollowComponentBuilder
{
    // Panel IDs are "{id}:{ownerId}:{followId}:{listPage}". Only the owner (who ran the command) can use the
    // panel, the list page lets the panel go back to the list
    public const string ChannelMenuId = "follow_channel";
    public const string NewProjectChannelMenuId = "follow_newchannel";
    public const string PingRoleMenuId = "follow_ping";
    public const string NewProjectPingRoleMenuId = "follow_newping";
    public const string MilestoneChannelMenuId = "follow_mschannel";
    public const string EventsMenuId = "follow_events";
    public const string ReleaseMenuId = "follow_release";
    public const string LoaderMenuId = "follow_loader";
    public const string OpenButtonId = "follow-open";
    public const string RemoveButtonId = "follow-remove";
    public const string RemoveConfirmButtonId = "follow-remove-confirm";
    public const string RemoveCancelButtonId = "follow-remove-cancel";
    public const string FollowUserButtonId = "follow-user";

    // Page of the follow's projects: "{id}:{ownerId}:{followId}:{listPage}:{page}:{action}" and the toggle
    // "{id}:{ownerId}:{followId}:{listPage}:{page}:{projectId}"
    public const string IgnoredProjectsButtonId = "follow-ign";
    public const string IgnoreToggleButtonId = "follow-ign-t";

    /// <param name="loaders">Loaders to offer in the loader filter, no menu is added if empty</param>
    /// <param name="listPage">List page the panel was opened from, carried in all IDs of the panel</param>
    /// <param name="backButton">Button back to the list</param>
    /// <param name="notice">Short note under the title, e.g. "Following X"</param>
    /// <param name="serverMilestones">If the server has milestones on, the milestone channel is hidden otherwise</param>
    public static MessageComponent GetSettingsPanel(ModrinthFollow follow, IEnumerable<string> loaders, ulong ownerId,
        int listPage, ButtonBuilder backButton, ILocalizationService loc, CultureInfo? culture, string? notice = null,
        bool serverMilestones = false)
    {
        string Id(string prefix) => $"{prefix}:{ownerId}:{follow.Id}:{listPage}";

        var fields = new List<SettingsField>
        {
            new(loc.Get("settings-channel", culture),
                SettingsPanelBuilder.ChannelPicker(Id(ChannelMenuId), follow.CustomUpdateChannel,
                    loc.Get("settings-channel-placeholder", culture))),
            new(loc.Get("settings-new-project-channel", culture),
                SettingsPanelBuilder.ChannelPicker(Id(NewProjectChannelMenuId), follow.NewProjectChannel,
                    loc.Get("settings-new-project-channel-placeholder", culture), optional: true)),
            new(loc.Get("settings-ping-role", culture),
                SettingsPanelBuilder.RolePicker(Id(PingRoleMenuId), follow.CustomPingRole,
                    loc.Get("settings-ping-role-placeholder", culture))),
            new(loc.Get("settings-new-project-ping-role", culture),
                SettingsPanelBuilder.RolePicker(Id(NewProjectPingRoleMenuId), follow.NewProjectPingRole,
                    loc.Get("settings-new-project-ping-role-placeholder", culture)))
        };

        // The channel does nothing while the server has milestones off or the follow lacks the milestone event
        if (serverMilestones && follow.Events.HasFlag(FollowEvents.DownloadMilestone))
            fields.Add(new SettingsField(loc.Get("settings-milestone-channel", culture),
                SettingsPanelBuilder.ChannelPicker(Id(MilestoneChannelMenuId), follow.MilestoneChannel,
                    loc.Get("settings-milestone-channel-placeholder", culture), optional: true)));

        fields.Add(new SettingsField(loc.Get("settings-events", culture),
            GetEventsMenu(follow, Id(EventsMenuId), loc, culture)));
        fields.Add(new SettingsField(loc.Get("settings-release-filter", culture),
            SettingsPanelBuilder.ReleaseFilterMenu(Id(ReleaseMenuId), follow.ReleaseFilter,
                loc.Get("settings-release-placeholder", culture))));

        var loaderMenu = SettingsPanelBuilder.LoaderFilterMenu(Id(LoaderMenuId), loaders, follow.LoaderFilter,
            loc.Get("settings-loader-placeholder", culture));
        if (loaderMenu is not null)
            fields.Add(new SettingsField(loc.Get("settings-loader-filter", culture), loaderMenu));

        var title = loc.Get("settings-follow-title", culture,
            new
            {
                name = Format.Sanitize(follow.TargetName),
                type = FollowEmbedBuilder.FormatTargetType(follow.TargetType, loc, culture)
            });

        var buttons = new[]
        {
            backButton,
            new ButtonBuilder(
                loc.Get("button-ignored-projects", culture, new
                {
                    ignored = follow.Projects.Count(fp => fp.Ignored), count = follow.Projects.Count
                }),
                $"{IgnoredProjectsButtonId}:{ownerId}:{follow.Id}:{listPage}:0:open", ButtonStyle.Secondary),
            new ButtonBuilder(loc.Get("button-unfollow", culture), Id(RemoveButtonId), ButtonStyle.Danger)
        };

        return SettingsPanelBuilder.Build(title, SettingsPanelBuilder.FollowAccent, fields, buttons, notice);
    }

    public static MessageComponent GetRemoveConfirmPanel(ModrinthFollow follow, ulong ownerId, int listPage,
        ILocalizationService loc, CultureInfo? culture)
    {
        return SettingsPanelBuilder.BuildMessage(
            loc.Get("follow-remove-confirm-question", culture, new { name = Format.Sanitize(follow.TargetName) }),
            SettingsPanelBuilder.FollowAccent,
            new ButtonBuilder(loc.Get("button-confirm", culture),
                $"{RemoveConfirmButtonId}:{ownerId}:{follow.Id}:{listPage}", ButtonStyle.Danger),
            new ButtonBuilder(loc.Get("button-cancel", culture),
                $"{RemoveCancelButtonId}:{ownerId}:{follow.Id}:{listPage}", ButtonStyle.Secondary));
    }

    public static ButtonBuilder GetFollowUserButton(User user, ILocalizationService loc, CultureInfo? culture)
    {
        return new ButtonBuilder(
            loc.Get("follow-button-follow-user", culture),
            $"{FollowUserButtonId}:{user.Id}",
            ButtonStyle.Success,
            emote: Emoji.Parse(":bell:"));
    }

    private static SelectMenuBuilder GetEventsMenu(ModrinthFollow follow, string customId, ILocalizationService loc,
        CultureInfo? culture)
    {
        return AddEventOptions(new SelectMenuBuilder()
            .WithCustomId(customId)
            .WithPlaceholder(loc.Get("settings-events-placeholder", culture))
            .WithMinValues(0), follow.Events, loc, culture);
    }

    /// <summary>
    ///     Adds an option with a description for every event, the values are the event flags
    /// </summary>
    /// <param name="selected">Events selected by default</param>
    public static SelectMenuBuilder AddEventOptions(SelectMenuBuilder menu, FollowEvents selected,
        ILocalizationService loc, CultureInfo? culture)
    {
        (FollowEvents Event, string Key)[] events =
        [
            (FollowEvents.ProjectUpdate, "follow-event-project-update"),
            (FollowEvents.NewProject, "follow-event-new-project"),
            (FollowEvents.DownloadMilestone, "follow-event-download-milestone")
        ];

        foreach (var (followEvent, key) in events)
            menu.AddOption(loc.Get(key, culture), ((int)followEvent).ToString(),
                loc.Get($"{key}-description", culture), isDefault: selected.HasFlag(followEvent));

        return menu.WithMaxValues(events.Length);
    }
}
