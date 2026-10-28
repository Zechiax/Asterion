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
///     Buttons and menus of the follow settings panel and its page of projects to ignore. The panel replaces the
///     manage panel it was opened from, the list page in the custom IDs lets it go back. Only the owner (who ran the
///     command) can use it. Kept outside the "follow" group so the custom IDs are not prefixed with the group name.
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
    IGuildService guildService,
    ISubscriptionService subscriptionService)
    : AsterionInteractionModuleBase(localizationService, cultureProvider)
{
    public static Task<IReadOnlyList<string>> GetLoaderOptionsAsync(IFollowService followService,
        ModrinthFollow follow)
    {
        return followService.GetTargetLoadersAsync(follow.TargetType, follow.TargetId);
    }

    /// <summary>
    ///     Settings panel of a follow, send it with <see cref="MessageFlags.ComponentsV2" />
    /// </summary>
    /// <param name="listPage">List page the panel was opened from</param>
    /// <param name="ownerId">The only user who can use the panel</param>
    /// <param name="notice">Short note under the title, e.g. "Following X"</param>
    public static async Task<MessageComponent> BuildSettingsPanelAsync(IFollowService followService,
        IGuildService guildService, ModrinthFollow follow, ulong ownerId, int listPage, ILocalizationService loc,
        CultureInfo? culture, string? notice = null)
    {
        var loaders = await GetLoaderOptionsAsync(followService, follow);
        var backButton = ManageInteractionModule.BackButton(ownerId, ManageTab.Creators, listPage, loc, culture);
        var serverMilestones = await guildService.MilestonesEnabledAsync(follow.GuildId);

        return FollowComponentBuilder.GetSettingsPanel(follow, loaders, ownerId, listPage, backButton, loc, culture,
            notice, serverMilestones);
    }

    /// <summary>
    ///     Page of the follow's projects with a button to ignore or unignore each,
    ///     send it with <see cref="MessageFlags.ComponentsV2" />
    /// </summary>
    /// <param name="listPage">List page the follow's panel was opened from, to go back to it</param>
    /// <param name="page">Page of the projects</param>
    public static MessageComponent BuildIgnoredProjectsPage(ModrinthFollow follow, ulong ownerId, int listPage,
        int page, ILocalizationService loc, CultureInfo? culture, string? notice = null)
    {
        var projects = follow.Projects
            .OrderBy(fp => fp.Project?.Title ?? fp.ProjectId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(fp => fp.ProjectId)
            .ToList();

        // Clamp here too, the toggle buttons need the page actually shown to come back to it
        var shownPage = Math.Clamp(page, 0, ListPageBuilder.GetPageCount(projects.Count) - 1);

        return ListPageBuilder.Build(projects, shownPage,
            loc.Get("follow-projects-title", culture, new { name = Format.Sanitize(follow.TargetName) }),
            loc.Get("follow-projects-description", culture),
            SettingsPanelBuilder.FollowAccent,
            $"{FollowComponentBuilder.IgnoredProjectsButtonId}:{ownerId}:{follow.Id}:{listPage}",
            fp => loc.Get(fp.Ignored ? "follow-project-entry-ignored" : "follow-project-entry", culture,
                new { title = Format.Sanitize(fp.Project?.Title ?? fp.ProjectId), id = fp.ProjectId }),
            fp => new ButtonBuilder(loc.Get(fp.Ignored ? "button-unignore" : "button-ignore", culture),
                $"{FollowComponentBuilder.IgnoreToggleButtonId}:{ownerId}:{follow.Id}:{listPage}:{shownPage}:{fp.ProjectId}",
                fp.Ignored ? ButtonStyle.Success : ButtonStyle.Secondary),
            loc.Get("follow-projects-empty", culture), loc, culture, notice,
            [
                new ButtonBuilder(loc.Get("button-back", culture),
                    $"{FollowComponentBuilder.OpenButtonId}:{ownerId}:{follow.Id}:{listPage}", ButtonStyle.Secondary)
            ]).Components;
    }

    [ComponentInteraction(FollowComponentBuilder.IgnoredProjectsButtonId + ":*:*:*:*:*")]
    public async Task HandleIgnoredProjectsAsync(ulong ownerId, int followId, int listPage, int page, string _)
    {
        await DeferAsync();

        var follow = await followService.GetFollowAsync(Context.Guild.Id, followId);
        if (follow is null)
        {
            await ShowResultAsync(ownerId, listPage, LocalizationService.Get("follow-not-found", CommandCultureInfo));
            return;
        }

        await ModifyOriginalResponseAsync(msg => msg.Components = BuildIgnoredProjectsPage(follow, ownerId, listPage,
            page, LocalizationService, CommandCultureInfo));
    }

    [ComponentInteraction(FollowComponentBuilder.IgnoreToggleButtonId + ":*:*:*:*:*")]
    public async Task HandleIgnoreToggleAsync(ulong ownerId, int followId, int listPage, int page, string projectId)
    {
        await DeferAsync();

        var follow = await followService.GetFollowAsync(Context.Guild.Id, followId);
        if (follow is null)
        {
            await ShowResultAsync(ownerId, listPage, LocalizationService.Get("follow-not-found", CommandCultureInfo));
            return;
        }

        var followProject = follow.Projects.FirstOrDefault(fp => fp.ProjectId == projectId);
        string? notice = null;
        if (followProject is null ||
            !await followService.SetIgnoredAsync(Context.Guild.Id, followId, projectId, !followProject.Ignored))
        {
            // The project was removed from the follow since the page was shown
            notice = LocalizationService.Get("follow-project-not-found", CommandCultureInfo,
                new { name = Format.Sanitize(follow.TargetName), project = Format.Sanitize(projectId) });
        }

        // Reload, the follow service saves through its own context
        follow = await followService.GetFollowAsync(Context.Guild.Id, followId) ?? follow;

        await ModifyOriginalResponseAsync(msg => msg.Components = BuildIgnoredProjectsPage(follow, ownerId, listPage,
            page, LocalizationService, CommandCultureInfo, notice));
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

        var removed = await followService.RemoveFollowAsync(Context.Guild.Id, followId);
        if (removed is null)
        {
            await ShowResultAsync(ownerId, listPage, LocalizationService.Get("follow-not-found", CommandCultureInfo));
            return;
        }

        await ShowResultAsync(ownerId, listPage, LocalizationService.Get("follow-removed", CommandCultureInfo,
            new { name = Format.Sanitize(removed.TargetName) }));
    }

    [ComponentInteraction(FollowComponentBuilder.RemoveCancelButtonId + ":*:*:*")]
    public Task HandleRemoveCancelAsync(ulong ownerId, int followId, int listPage)
    {
        return UpdateAsync(ownerId, followId, listPage, _ => { });
    }

    [ComponentInteraction(FollowComponentBuilder.ChannelMenuId + ":*:*:*")]
    public async Task HandleChannelSelectionAsync(ulong ownerId, int followId, int listPage, string[] selectedValues)
    {
        // The picker requires a channel, redraw the saved state in case nothing was selected
        var channelId = SettingsPanelBuilder.ParseSelectedId(selectedValues);
        if (channelId is null)
        {
            await UpdateAsync(ownerId, followId, listPage, _ => { });
            return;
        }

        await SetChannelAsync(ownerId, followId, listPage, channelId.Value, f => f.CustomUpdateChannel = channelId);
    }

    [ComponentInteraction(FollowComponentBuilder.NewProjectChannelMenuId + ":*:*:*")]
    public async Task HandleNewProjectChannelSelectionAsync(ulong ownerId, int followId, int listPage,
        string[] selectedValues)
    {
        var channelId = SettingsPanelBuilder.ParseSelectedId(selectedValues);

        // Deselecting sends new projects to the update channel again
        if (channelId is null)
        {
            await UpdateAsync(ownerId, followId, listPage, f => f.NewProjectChannel = null);
            return;
        }

        await SetChannelAsync(ownerId, followId, listPage, channelId.Value, f => f.NewProjectChannel = channelId);
    }

    [ComponentInteraction(FollowComponentBuilder.MilestoneChannelMenuId + ":*:*:*")]
    public async Task HandleMilestoneChannelSelectionAsync(ulong ownerId, int followId, int listPage,
        string[] selectedValues)
    {
        var channelId = SettingsPanelBuilder.ParseSelectedId(selectedValues);

        // Deselecting sends milestones to the server's milestone channel again
        if (channelId is null)
        {
            await UpdateAsync(ownerId, followId, listPage, f => f.MilestoneChannel = null);
            return;
        }

        await SetChannelAsync(ownerId, followId, listPage, channelId.Value, f => f.MilestoneChannel = channelId);
    }

    /// <summary>
    ///     Saves the channel if the bot can send messages there, otherwise shows the saved state with a warning
    /// </summary>
    private async Task SetChannelAsync(ulong ownerId, int followId, int listPage, ulong channelId,
        Action<ModrinthFollow> update)
    {
        if (!BotCanSendIn(channelId))
        {
            // Show the panel again, so the picker goes back to the saved channel
            await UpdateAsync(ownerId, followId, listPage, _ => { });
            await SendStatusAsync(Warnings.BotCannotSend(MentionUtils.MentionChannel(channelId)));
            return;
        }

        await UpdateAsync(ownerId, followId, listPage, update);
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
    public Task HandleReleaseFilterSelectionAsync(ulong ownerId, int followId, int listPage, string[] selectedValues)
    {
        // The menu requires at least one release type
        ReleaseType releaseFilter = 0;
        foreach (var value in selectedValues)
            if (int.TryParse(value, out var flag))
                releaseFilter |= (ReleaseType)flag;

        return UpdateAsync(ownerId, followId, listPage, f => f.ReleaseFilter = releaseFilter);
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
        var panel = await BuildSettingsPanelAsync(followService, guildService, follow, ownerId, listPage,
            LocalizationService, CommandCultureInfo);
        await ModifyOriginalResponseAsync(msg => msg.Components = panel);
    }

    /// <summary>
    ///     Goes back to the list with the text as a note
    /// </summary>
    private async Task ShowResultAsync(ulong ownerId, int listPage, string text)
    {
        var components = await ManageInteractionModule.BuildPanelAsync(subscriptionService, guildService,
            followService, Context.Guild.Id, ownerId, ManageTab.Creators, listPage, CanManageServerHere,
            LocalizationService, CommandCultureInfo, text);

        await ModifyOriginalResponseAsync(msg => msg.Components = components);
    }
}
