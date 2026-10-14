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
    public const string PingRoleMenuId = "follow_ping";
    public const string NewProjectPingRoleMenuId = "follow_newping";
    public const string EventsMenuId = "follow_events";
    public const string ReleaseMenuId = "follow_release";
    public const string LoaderMenuId = "follow_loader";
    public const string OpenButtonId = "follow-open";
    public const string RemoveButtonId = "follow-remove";
    public const string RemoveConfirmButtonId = "follow-remove-confirm";
    public const string RemoveCancelButtonId = "follow-remove-cancel";
    public const string FollowUserButtonId = "follow-user";

    /// <param name="loaders">Loaders to offer in the loader filter, no menu is added if empty</param>
    /// <param name="listPage">List page the panel was opened from, carried in all IDs of the panel</param>
    /// <param name="backButton">Button back to the list, null if the panel was not opened from it</param>
    public static MessageComponent GetSettingsPanel(ModrinthFollow follow, IEnumerable<string> loaders, ulong ownerId,
        int listPage, ButtonBuilder? backButton, ILocalizationService loc, CultureInfo? culture)
    {
        string Id(string prefix) => $"{prefix}:{ownerId}:{follow.Id}:{listPage}";

        var fields = new List<SettingsField>
        {
            new(loc.Get("settings-channel", culture),
                SettingsPanelBuilder.ChannelPicker(Id(ChannelMenuId), follow.CustomUpdateChannel,
                    loc.Get("settings-channel-placeholder", culture))),
            new(loc.Get("settings-ping-role", culture),
                SettingsPanelBuilder.RolePicker(Id(PingRoleMenuId), follow.CustomPingRole,
                    loc.Get("settings-ping-role-placeholder", culture))),
            new(loc.Get("settings-new-project-ping-role", culture),
                SettingsPanelBuilder.RolePicker(Id(NewProjectPingRoleMenuId), follow.NewProjectPingRole,
                    loc.Get("settings-new-project-ping-role-placeholder", culture))),
            new(loc.Get("settings-events", culture), GetEventsMenu(follow, Id(EventsMenuId), loc, culture)),
            new(loc.Get("settings-release-filter", culture),
                SettingsPanelBuilder.ReleaseFilterMenu(Id(ReleaseMenuId), follow.ReleaseFilter,
                    loc.Get("settings-release-placeholder", culture)))
        };

        var loaderMenu = SettingsPanelBuilder.LoaderFilterMenu(Id(LoaderMenuId), loaders, follow.LoaderFilter,
            loc.Get("settings-loader-placeholder", culture));
        if (loaderMenu is not null)
            fields.Add(new SettingsField(loc.Get("settings-loader-filter", culture), loaderMenu));

        var title = loc.Get("settings-follow-title", culture,
            new { name = follow.TargetName, type = FollowEmbedBuilder.FormatTargetType(follow.TargetType, loc, culture) });

        var buttons = new List<ButtonBuilder>();
        if (backButton is not null)
            buttons.Add(backButton);
        buttons.Add(new ButtonBuilder(loc.Get("button-unfollow", culture), Id(RemoveButtonId), ButtonStyle.Danger));

        return SettingsPanelBuilder.Build(title, SettingsPanelBuilder.FollowAccent, fields, buttons);
    }

    public static MessageComponent GetRemoveConfirmPanel(ModrinthFollow follow, ulong ownerId, int listPage,
        ILocalizationService loc, CultureInfo? culture)
    {
        return SettingsPanelBuilder.BuildMessage(
            loc.Get("follow-remove-confirm-question", culture, new { name = follow.TargetName }),
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
        return new SelectMenuBuilder()
            .WithCustomId(customId)
            .WithPlaceholder(loc.Get("settings-events-placeholder", culture))
            .WithMinValues(0)
            .WithMaxValues(2)
            .AddOption(loc.Get("follow-event-project-update", culture), ((int)FollowEvents.ProjectUpdate).ToString(),
                isDefault: follow.Events.HasFlag(FollowEvents.ProjectUpdate))
            .AddOption(loc.Get("follow-event-new-project", culture), ((int)FollowEvents.NewProject).ToString(),
                isDefault: follow.Events.HasFlag(FollowEvents.NewProject));
    }
}
