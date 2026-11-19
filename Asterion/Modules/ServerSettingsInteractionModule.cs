using Asterion.Attributes;
using Asterion.Common;
using Asterion.ComponentBuilders;
using Asterion.Database.Models;
using Asterion.EmbedBuilders;
using Asterion.Interfaces;
using Asterion.Services.Modrinth;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Asterion.Modules;

/// <summary>
///     Menus and buttons of the server tabs of the manage panel, administrators only
/// </summary>
[RequireUserPermission(GuildPermission.Administrator)]
[RequireContext(ContextType.Guild)]
// ReSharper disable once ClassNeverInstantiated.Global
public class ServerSettingsInteractionModule(
    ILocalizationService localizationService,
    IGuildCultureProvider cultureProvider,
    IGuildService guildService,
    ISubscriptionService subscriptionService,
    IFollowService followService,
    ModrinthService modrinthService)
    : AsterionInteractionModuleBase(localizationService, cultureProvider)
{
    private const int ProjectBatchSize = 100;
    [DoUserCheck]
    [ComponentInteraction(ServerSettingsPanelBuilder.ManageRoleMenuId + ":*")]
    public Task HandleManageRoleAsync(ulong ownerId, string[] selectedValues)
    {
        var roleId = SettingsPanelBuilder.ParseSelectedId(selectedValues);
        return UpdateAsync(ownerId, ManageTab.General, guild => guild.ManageRole = roleId);
    }

    [DoUserCheck]
    [ComponentInteraction(ServerSettingsPanelBuilder.PingRoleMenuId + ":*")]
    public Task HandlePingRoleAsync(ulong ownerId, string[] selectedValues)
    {
        var roleId = SettingsPanelBuilder.ParseSelectedId(selectedValues);
        return UpdateAsync(ownerId, ManageTab.General, guild => guild.PingRole = roleId);
    }

    [DoUserCheck]
    [ComponentInteraction(ServerSettingsPanelBuilder.LanguageMenuId + ":*")]
    public async Task HandleLanguageAsync(ulong ownerId, string[] selectedValues)
    {
        var selected = selectedValues.FirstOrDefault();
        var language = selected is null or ServerSettingsPanelBuilder.DefaultLanguageValue ? null : selected;

        await UpdateAsync(ownerId, ManageTab.General, guild => guild.GuildSettings.Language = language,
            RefreshCultureAsync);
    }

    [DoUserCheck]
    [ComponentInteraction(ServerSettingsPanelBuilder.MessageStyleMenuId + ":*")]
    public Task HandleMessageStyleAsync(ulong ownerId, string[] selectedValues)
    {
        return Enum.TryParse<MessageStyle>(selectedValues.FirstOrDefault(), out var style)
            ? UpdateAsync(ownerId, ManageTab.Notifications, guild => guild.GuildSettings.MessageStyle = style)
            : DeferAsync();
    }

    [DoUserCheck]
    [ComponentInteraction(ServerSettingsPanelBuilder.ChangelogStyleMenuId + ":*")]
    public Task HandleChangelogStyleAsync(ulong ownerId, string[] selectedValues)
    {
        return Enum.TryParse<ChangelogStyle>(selectedValues.FirstOrDefault(), out var style)
            ? UpdateAsync(ownerId, ManageTab.Notifications, guild => guild.GuildSettings.ChangelogStyle = style)
            : DeferAsync();
    }

    [DoUserCheck]
    [ComponentInteraction(ServerSettingsPanelBuilder.MilestoneToggleId + ":*")]
    public Task HandleMilestoneToggleAsync(ulong ownerId)
    {
        return UpdateAsync(ownerId, ManageTab.Notifications,
            guild => guild.GuildSettings.MilestonesEnabled = !guild.GuildSettings.MilestonesEnabled);
    }

    [DoUserCheck]
    [ComponentInteraction(ServerSettingsPanelBuilder.MilestoneLadderMenuId + ":*")]
    public Task HandleMilestoneLadderAsync(ulong ownerId, string[] selectedValues)
    {
        return Enum.TryParse<MilestoneLadder>(selectedValues.FirstOrDefault(), out var ladder)
            ? UpdateAsync(ownerId, ManageTab.Notifications, guild => guild.GuildSettings.MilestoneLadder = ladder)
            : DeferAsync();
    }

    [DoUserCheck]
    [ComponentInteraction(ServerSettingsPanelBuilder.MilestoneChannelMenuId + ":*")]
    public async Task HandleMilestoneChannelAsync(ulong ownerId, string[] selectedValues)
    {
        // Deselecting sends milestones to the channel of each subscription or follow again
        var channelId = SettingsPanelBuilder.ParseSelectedId(selectedValues);

        if (channelId is not null && !BotCanSendIn(channelId.Value))
        {
            // Show the tab again, so the picker goes back to the saved channel
            await UpdateAsync(ownerId, ManageTab.Notifications);
            await SendStatusAsync(Warnings.BotCannotSend(MentionUtils.MentionChannel(channelId.Value)));
            return;
        }

        await UpdateAsync(ownerId, ManageTab.Notifications, guild => guild.GuildSettings.MilestoneChannel = channelId);
    }

    [DoUserCheck]
    [ComponentInteraction(ServerSettingsPanelBuilder.MilestonePingRoleMenuId + ":*")]
    public Task HandleMilestonePingRoleAsync(ulong ownerId, string[] selectedValues)
    {
        var roleId = SettingsPanelBuilder.ParseSelectedId(selectedValues);
        return UpdateAsync(ownerId, ManageTab.Notifications, guild => guild.GuildSettings.MilestonePingRole = roleId);
    }

    [DoUserCheck]
    [ComponentInteraction(ServerSettingsPanelBuilder.DefaultFiltersButtonId + ":*")]
    public async Task HandleDefaultFiltersAsync(ulong ownerId)
    {
        await DeferAsync();
        await ShowDefaultFiltersAsync(ownerId);
    }

    [DoUserCheck]
    [ComponentInteraction(ServerSettingsPanelBuilder.DefaultReleaseMenuId + ":*")]
    public async Task HandleDefaultReleaseFilterAsync(ulong ownerId, string[] selectedValues)
    {
        await DeferAsync();

        var filter = SettingsPanelBuilder.ParseReleaseFilter(selectedValues);
        if (filter is not null &&
            await guildService.UpdateGuildAsync(Context.Guild.Id,
                guild => guild.GuildSettings.DefaultReleaseFilter = filter.Value) is null)
        {
            await SendStatusAsync(Errors.Internal);
            return;
        }

        await ShowDefaultFiltersAsync(ownerId);
    }

    [DoUserCheck]
    [ComponentInteraction(ServerSettingsPanelBuilder.DefaultLoaderMenuId + ":*")]
    public async Task HandleDefaultLoaderFilterAsync(ulong ownerId, string[] selectedValues)
    {
        await DeferAsync();

        var loaderFilter = selectedValues.Length > 0 ? selectedValues : null;
        if (await guildService.UpdateGuildAsync(Context.Guild.Id,
                guild => guild.GuildSettings.DefaultLoaderFilter = loaderFilter) is null)
        {
            await SendStatusAsync(Errors.Internal);
            return;
        }

        await ShowDefaultFiltersAsync(ownerId);
    }

    [DoUserCheck]
    [ComponentInteraction(ServerSettingsPanelBuilder.ResetOverridesButtonId + ":*")]
    public async Task HandleResetOverridesAsync(ulong ownerId)
    {
        await DeferAsync();

        var count = await guildService.CountFilterOverridesAsync(Context.Guild.Id);
        if (count == 0)
        {
            await ShowDefaultFiltersAsync(ownerId);
            return;
        }

        var confirm = ServerSettingsPanelBuilder.BuildResetOverridesConfirm(count, ownerId, LocalizationService,
            CommandCultureInfo);
        await ModifyOriginalResponseAsync(msg => msg.Components = confirm);
    }

    [DoUserCheck]
    [ComponentInteraction(ServerSettingsPanelBuilder.ResetOverridesConfirmButtonId + ":*")]
    public async Task HandleResetOverridesConfirmAsync(ulong ownerId)
    {
        await DeferAsync();

        var count = await guildService.ResetFilterOverridesAsync(Context.Guild.Id);
        await ShowDefaultFiltersAsync(ownerId, Loc("server-reset-overrides-done", new { count }));
    }

    [DoUserCheck]
    [ComponentInteraction(ServerSettingsPanelBuilder.ResetOverridesCancelButtonId + ":*")]
    public async Task HandleResetOverridesCancelAsync(ulong ownerId)
    {
        await DeferAsync();
        await ShowDefaultFiltersAsync(ownerId);
    }

    [DoUserCheck]
    [ComponentInteraction(ServerSettingsPanelBuilder.SubscribeButtonToggleId + ":*")]
    public Task HandleSubscribeButtonToggleAsync(ulong ownerId)
    {
        return UpdateAsync(ownerId, ManageTab.General,
            guild => guild.GuildSettings.ShowSubscribeButton = !(guild.GuildSettings.ShowSubscribeButton ?? true));
    }

    [DoUserCheck]
    [ComponentInteraction(ServerSettingsPanelBuilder.ChangelogLengthButtonId + ":*")]
    public async Task HandleChangelogLengthAsync(ulong ownerId)
    {
        var guild = await guildService.GetGuildAsync(Context.Guild.Id);
        if (guild is null)
        {
            await SendStatusAsync(Errors.Internal);
            return;
        }

        await RespondWithModalAsync(ServerSettingsPanelBuilder.BuildChangelogLengthModal(
            guild.GuildSettings.ChangeLogMaxLength, LocalizationService, CommandCultureInfo));
    }

    [ModalInteraction(ServerSettingsPanelBuilder.ChangelogLengthModalId)]
    public async Task HandleChangelogLengthSubmitAsync(RawModal _)
    {
        var modal = (SocketModal)Context.Interaction;
        var length = ServerSettingsPanelBuilder.ParseChangelogLength(
            modal.GetText(ServerSettingsPanelBuilder.ChangelogLengthInputId));

        if (length is null)
        {
            await SendStatusAsync(Errors.ChangelogLengthInvalid(ServerSettingsPanelBuilder.MinChangelogLength,
                ServerSettingsPanelBuilder.MaxChangelogLength));
            return;
        }

        // Only the owner of the panel could open the modal
        await UpdateAsync(Context.User.Id, ManageTab.Notifications,
            guild => guild.GuildSettings.ChangeLogMaxLength = length.Value);
    }

    [DoUserCheck]
    [ComponentInteraction(ServerSettingsPanelBuilder.PreviewButtonId + ":*")]
    public async Task HandlePreviewAsync(ulong ownerId)
    {
        await DeferAsync();

        var guild = await guildService.GetGuildAsync(Context.Guild.Id);
        if (guild is null)
        {
            await SendStatusAsync(Errors.Internal);
            return;
        }

        await FollowupAsync(embed: SettingsEmbedBuilder.GetViewSettingsEmbed(guild).Build(), ephemeral: true);
    }

    [DoUserCheck]
    [ComponentInteraction(ServerSettingsPanelBuilder.UnsubscribeAllButtonId + ":*")]
    public async Task HandleUnsubscribeAllAsync(ulong ownerId)
    {
        var count = (await subscriptionService.GetSubscriptionsAsync(Context.Guild.Id)).Count;

        await RespondWithModalAsync(ServerSettingsPanelBuilder.BuildDangerModal(
            ServerSettingsPanelBuilder.UnsubscribeAllModalId, "server-unsubscribe-all", count, LocalizationService,
            CommandCultureInfo));
    }

    [ModalInteraction(ServerSettingsPanelBuilder.UnsubscribeAllModalId)]
    public async Task HandleUnsubscribeAllSubmitAsync(RawModal _)
    {
        if (!await CheckConfirmedAsync("server-unsubscribe-all"))
            return;

        await DeferAsync();

        var count = await subscriptionService.RemoveAllSubscriptionsAsync(Context.Guild.Id);

        // Only the owner of the panel could open the modal
        await ShowPanelAsync(Context.User.Id, ManageTab.General, Loc("server-unsubscribed-all", new { count }));
    }

    [DoUserCheck]
    [ComponentInteraction(ServerSettingsPanelBuilder.UnfollowAllButtonId + ":*")]
    public async Task HandleUnfollowAllAsync(ulong ownerId)
    {
        var count = await followService.CountFollowsAsync(Context.Guild.Id);

        await RespondWithModalAsync(ServerSettingsPanelBuilder.BuildDangerModal(
            ServerSettingsPanelBuilder.UnfollowAllModalId, "server-unfollow-all", count, LocalizationService,
            CommandCultureInfo));
    }

    [ModalInteraction(ServerSettingsPanelBuilder.UnfollowAllModalId)]
    public async Task HandleUnfollowAllSubmitAsync(RawModal _)
    {
        if (!await CheckConfirmedAsync("server-unfollow-all"))
            return;

        await DeferAsync();

        var count = await followService.RemoveAllFollowsAsync(Context.Guild.Id);

        // Only the owner of the panel could open the modal
        await ShowPanelAsync(Context.User.Id, ManageTab.General, Loc("server-unfollowed-all", new { count }));
    }

    /// <summary>
    ///     If the confirmation word was typed in the danger modal, otherwise tells the user nothing was removed
    /// </summary>
    private async Task<bool> CheckConfirmedAsync(string keyPrefix)
    {
        var word = Loc($"{keyPrefix}-word");
        var input = ((SocketModal)Context.Interaction).GetText(ServerSettingsPanelBuilder.ConfirmInputId);

        if (ServerSettingsPanelBuilder.IsConfirmed(input, word, CommandCultureInfo))
            return true;

        await SendStatusAsync(Errors.DangerNotConfirmed(word));
        return false;
    }

    /// <summary>
    ///     Applies a change to the guild and shows the tab with the saved state
    /// </summary>
    /// <param name="update">The change, null to only show the tab</param>
    /// <param name="afterSave">Runs after the change is saved, before the panel is built</param>
    private async Task UpdateAsync(ulong ownerId, ManageTab tab, Action<Guild>? update = null,
        Func<Task>? afterSave = null)
    {
        await DeferAsync();

        if (update is not null && await guildService.UpdateGuildAsync(Context.Guild.Id, update) is null)
        {
            await SendStatusAsync(Errors.Internal);
            return;
        }

        if (afterSave is not null)
            await afterSave();

        await ShowPanelAsync(ownerId, tab);
    }

    private async Task ShowPanelAsync(ulong ownerId, ManageTab tab, string? notice = null)
    {
        var components = await ManageInteractionModule.BuildPanelAsync(subscriptionService, guildService,
            followService, Context.Guild.Id, ownerId, tab, 0, true, LocalizationService, CommandCultureInfo, notice);

        await ModifyOriginalResponseAsync(msg => msg.Components = components);
    }

    private async Task ShowDefaultFiltersAsync(ulong ownerId, string? notice = null)
    {
        var guild = await guildService.GetGuildAsync(Context.Guild.Id);
        if (guild is null)
        {
            await SendStatusAsync(Errors.Internal);
            return;
        }

        var loaders = await GetServerLoaderOptionsAsync(Context.Guild.Id);
        var overrideCount = await guildService.CountFilterOverridesAsync(Context.Guild.Id);
        var panel = ServerSettingsPanelBuilder.BuildDefaultFilters(guild.GuildSettings, loaders, overrideCount, ownerId,
            LocalizationService, CommandCultureInfo, notice);

        await ModifyOriginalResponseAsync(msg => msg.Components = panel);
    }

    /// <summary>
    ///     Collects loader options for the guild's default loader menu in priority order:
    ///     loaders used by the guild's subscriptions and follows first, then all Modrinth loaders.
    /// </summary>
    private async Task<IReadOnlyList<string>> GetServerLoaderOptionsAsync(ulong guildId)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddRange(IEnumerable<string>? names)
        {
            if (names is null)
                return;

            foreach (var name in names)
                if (!string.IsNullOrWhiteSpace(name) && seen.Add(name))
                    result.Add(name);
        }

        var entries = await subscriptionService.GetSubscriptionsAsync(guildId);
        if (entries.Count > 0)
        {
            foreach (var batch in entries.Select(e => e.ProjectId).Distinct().Chunk(ProjectBatchSize))
            {
                var projects = await modrinthService.GetMultipleProjects(batch);
                if (projects is not null)
                    AddRange(projects.SelectMany(p => p.Loaders ?? []));
            }
        }

        var follows = await followService.GetFollowsAsync(guildId);
        foreach (var follow in follows)
            AddRange(await followService.GetTargetLoadersAsync(follow.TargetType, follow.TargetId));

        AddRange(SettingsPanelBuilder.MainLoaders);

        var allLoaders = await modrinthService.GetLoadersAsync();
        if (allLoaders is not null)
            AddRange(allLoaders.Select(l => l.Name));

        return result;
    }

    private string Loc(string key)
    {
        return LocalizationService.Get(key, CommandCultureInfo);
    }

    private string Loc(string key, object args)
    {
        return LocalizationService.Get(key, CommandCultureInfo, args);
    }
}

/// <summary>
///     Buttons and menus of messages of the old <c>/settings</c> command, they point to <c>/manage</c> now
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
public class LegacySettingsInteractionModule(
    ILocalizationService localizationService,
    IGuildCultureProvider cultureProvider)
    : AsterionInteractionModuleBase(localizationService, cultureProvider)
{
    [ComponentInteraction("settings-main:*")]
    public Task MainScreen(string _) => RespondOutdatedAsync();

    [ComponentInteraction("settings-module-more:*")]
    public Task More(string _) => RespondOutdatedAsync();

    [ComponentInteraction("settings-module-notification-style:*")]
    public Task NotificationStyle(string _) => RespondOutdatedAsync();

    [ComponentInteraction("change-message-style:*")]
    public Task MessageStyle(string _, string[] __) => RespondOutdatedAsync();

    [ComponentInteraction("change-changelog-style:*")]
    public Task ChangelogStyle(string _, string[] __) => RespondOutdatedAsync();

    [ComponentInteraction("settings-message-scan:*;*")]
    public Task MessageScan(string _, string __) => RespondOutdatedAsync();

    [ComponentInteraction("settings-show-subscribe-button:*;*")]
    public Task ShowSubscribeButton(string _, string __) => RespondOutdatedAsync();

    private Task RespondOutdatedAsync()
    {
        return SendStatusAsync(Errors.MenuOutdated);
    }
}
