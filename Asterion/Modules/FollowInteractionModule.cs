using System.Globalization;
using Asterion.Attributes;
using Asterion.Common;
using Asterion.ComponentBuilders;
using Asterion.Database.Models;
using Asterion.Interfaces;
using Discord;
using Discord.Interactions;

namespace Asterion.Modules;

/// <summary>
///     Buttons and menus of the follow settings panel. The panel replaces the list message it was opened from,
///     the list page in the custom IDs lets it go back. Only the owner (who ran the command) can use it. Kept outside the "follow" group so the custom IDs are
///     not prefixed with the group name.
/// </summary>
[RequireContext(ContextType.Guild)]
[RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
[DoManageSubsRoleCheck(Group = "ManageSubs")]
[DoUserCheck]
// ReSharper disable once ClassNeverInstantiated.Global
public class FollowInteractionModule(
    ILocalizationService localizationService,
    IGuildCultureProvider cultureProvider,
    IFollowService followService,
    IDataService dataService)
    : AsterionInteractionModuleBase(localizationService, cultureProvider)
{
    public static async Task<IEnumerable<string>> GetLoaderOptionsAsync(IFollowService followService,
        ModrinthFollow follow)
    {
        var source = await followService.FetchTargetAsync(follow.TargetType, follow.TargetId);

        return source.Projects?.SelectMany(p => p.Loaders ?? []) ?? [];
    }

    /// <summary>
    ///     Settings panel of a follow, send it with <see cref="MessageFlags.ComponentsV2" />
    /// </summary>
    /// <param name="listPage">List page the panel was opened from, or <see cref="ListInteractionModule.NoList" /></param>
    /// <param name="ownerId">The only user who can use the panel</param>
    public static async Task<MessageComponent> BuildSettingsPanelAsync(IFollowService followService,
        ModrinthFollow follow, ulong ownerId, int listPage, ILocalizationService loc, CultureInfo? culture)
    {
        var loaders = await GetLoaderOptionsAsync(followService, follow);
        var backButton = listPage == ListInteractionModule.NoList
            ? null
            : ListInteractionModule.BackButton(ownerId, ListTab.Creators, listPage, loc, culture);

        return FollowComponentBuilder.GetSettingsPanel(follow, loaders, ownerId, listPage, backButton, loc, culture);
    }

    [ComponentInteraction(FollowComponentBuilder.OpenButtonId + ":*:*:*")]
    public async Task HandleOpenSettingsAsync(ulong ownerId, int followId, int listPage)
    {
        await DeferAsync();

        var follow = await followService.GetFollowAsync(Context.Guild.Id, followId);
        if (follow is null)
        {
            await ShowResultAsync(ownerId, listPage, LocalizationService.Get("follow-not-found", CommandCultureInfo));
            return;
        }

        await ShowPanelAsync(follow, ownerId, listPage);
    }

    [ComponentInteraction(FollowComponentBuilder.RemoveButtonId + ":*:*:*")]
    public async Task HandleRemoveAsync(ulong ownerId, int followId, int listPage)
    {
        await DeferAsync();

        var follow = await followService.GetFollowAsync(Context.Guild.Id, followId);
        if (follow is null)
        {
            await ShowResultAsync(ownerId, listPage, LocalizationService.Get("follow-not-found", CommandCultureInfo));
            return;
        }

        await ModifyOriginalResponseAsync(msg =>
            msg.Components = FollowComponentBuilder.GetRemoveConfirmPanel(follow, ownerId, listPage, LocalizationService,
                CommandCultureInfo));
    }

    [ComponentInteraction(FollowComponentBuilder.RemoveConfirmButtonId + ":*:*:*")]
    public async Task HandleRemoveConfirmAsync(ulong ownerId, int followId, int listPage)
    {
        await DeferAsync();

        var follow = await followService.GetFollowAsync(Context.Guild.Id, followId);
        if (follow is null || !await followService.RemoveFollowAsync(Context.Guild.Id, followId))
        {
            await ShowResultAsync(ownerId, listPage, LocalizationService.Get("follow-not-found", CommandCultureInfo));
            return;
        }

        await ShowResultAsync(ownerId, listPage, LocalizationService.Get("follow-removed", CommandCultureInfo,
            new { name = follow.TargetName }));
    }

    [ComponentInteraction(FollowComponentBuilder.RemoveCancelButtonId + ":*:*:*")]
    public Task HandleRemoveCancelAsync(ulong ownerId, int followId, int listPage)
    {
        return UpdateAsync(ownerId, followId, listPage, _ => { });
    }

    [ComponentInteraction(FollowComponentBuilder.ChannelMenuId + ":*:*:*")]
    public async Task HandleChannelSelectionAsync(ulong ownerId, int followId, int listPage, string[] selectedValues)
    {
        var channelId = SettingsPanelBuilder.ParseSelectedId(selectedValues);
        if (channelId is null)
            return;

        if (!BotCanSendIn(channelId.Value))
        {
            // Show the panel again, so the picker goes back to the saved channel
            await UpdateAsync(ownerId, followId, listPage, _ => { });
            await FollowupAsync(LocalizationService.Get("follow-no-send-permission", CommandCultureInfo,
                new { channel = MentionUtils.MentionChannel(channelId.Value) }), ephemeral: true);
            return;
        }

        await UpdateAsync(ownerId, followId, listPage, f => f.CustomUpdateChannel = channelId);
    }

    [ComponentInteraction(FollowComponentBuilder.PingRoleMenuId + ":*:*:*")]
    public Task HandlePingRoleSelectionAsync(ulong ownerId, int followId, int listPage, string[] selectedValues)
    {
        var roleId = SettingsPanelBuilder.ParseSelectedId(selectedValues);
        return UpdateAsync(ownerId, followId, listPage, f => f.CustomPingRole = roleId);
    }

    [ComponentInteraction(FollowComponentBuilder.NewProjectPingRoleMenuId + ":*:*:*")]
    public Task HandleNewProjectPingRoleSelectionAsync(ulong ownerId, int followId, int listPage, string[] selectedValues)
    {
        var roleId = SettingsPanelBuilder.ParseSelectedId(selectedValues);
        return UpdateAsync(ownerId, followId, listPage, f => f.NewProjectPingRole = roleId);
    }

    [ComponentInteraction(FollowComponentBuilder.EventsMenuId + ":*:*:*")]
    public Task HandleEventsSelectionAsync(ulong ownerId, int followId, int listPage, string[] selectedValues)
    {
        var events = FollowEvents.None;
        foreach (var value in selectedValues)
            if (int.TryParse(value, out var flag))
                events |= (FollowEvents)flag;

        return UpdateAsync(ownerId, followId, listPage, f => f.Events = events);
    }

    [ComponentInteraction(FollowComponentBuilder.ReleaseMenuId + ":*:*:*")]
    public async Task HandleReleaseFilterSelectionAsync(ulong ownerId, int followId, int listPage, string[] selectedValues)
    {
        ReleaseType releaseFilter = 0;
        foreach (var value in selectedValues)
            if (int.TryParse(value, out var flag))
                releaseFilter |= (ReleaseType)flag;

        if (releaseFilter == 0)
        {
            await RespondAsync(LocalizationService.Get("settings-release-required", CommandCultureInfo),
                ephemeral: true);
            return;
        }

        await UpdateAsync(ownerId, followId, listPage, f => f.ReleaseFilter = releaseFilter);
    }

    [ComponentInteraction(FollowComponentBuilder.LoaderMenuId + ":*:*:*")]
    public Task HandleLoaderFilterSelectionAsync(ulong ownerId, int followId, int listPage, string[] selectedValues)
    {
        return UpdateAsync(ownerId, followId, listPage,
            f => f.LoaderFilter = selectedValues.Length > 0 ? selectedValues : null);
    }

    private async Task UpdateAsync(ulong ownerId, int followId, int listPage, Action<ModrinthFollow> update)
    {
        await DeferAsync();

        var follow = await followService.UpdateSettingsAsync(Context.Guild.Id, followId, update);
        if (follow is null)
        {
            await ShowResultAsync(ownerId, listPage, LocalizationService.Get("follow-not-found", CommandCultureInfo));
            return;
        }

        await ShowPanelAsync(follow, ownerId, listPage);
    }

    private async Task ShowPanelAsync(ModrinthFollow follow, ulong ownerId, int listPage)
    {
        var panel = await BuildSettingsPanelAsync(followService, follow, ownerId, listPage, LocalizationService,
            CommandCultureInfo);
        await ModifyOriginalResponseAsync(msg => msg.Components = panel);
    }

    /// <summary>
    ///     Goes back to the list with the text as a note, or replaces the panel with the text if it was not
    ///     opened from the list
    /// </summary>
    private async Task ShowResultAsync(ulong ownerId, int listPage, string text)
    {
        var components = listPage == ListInteractionModule.NoList
            ? SettingsPanelBuilder.BuildMessage(text, SettingsPanelBuilder.FollowAccent)
            : (await ListInteractionModule.BuildListAsync(dataService, followService, Context.Guild.Id,
                ownerId, ListTab.Creators, listPage, LocalizationService, CommandCultureInfo, text)).Components;

        await ModifyOriginalResponseAsync(msg => msg.Components = components);
    }
}
