using System.Globalization;
using Asterion.Attributes;
using Asterion.Common;
using Asterion.ComponentBuilders;
using Asterion.Database.Models;
using Asterion.Interfaces;
using Discord;
using Discord.Interactions;
using Modrinth;
using Modrinth.Models;

namespace Asterion.Modules
{
    /// <summary>
    ///     The subscription settings panel. The panel replaces the manage panel it was opened from,
    ///     the list page in the custom IDs lets it go back.
    /// </summary>
    [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
    [DoManageSubsRoleCheck(Group = "ManageSubs")]
    public class EntryInteractionModule(
        ILocalizationService localizationService,
        IGuildCultureProvider cultureProvider,
        IDataService dataService,
        IFollowService followService,
        IModrinthClient modrinthClient)
        : AsterionInteractionModuleBase(localizationService, cultureProvider)
    {
        // Panel IDs are "{id}:{ownerId}:{projectId}:{listPage}". Only the owner (who ran the command) can use
        // the panel, the list page lets the panel go back to the list
        public const string OpenButtonId = "entry-open";
        public const string ChannelMenuId = "entry_channel";
        public const string PingRoleMenuId = "entry_ping";
        public const string ReleaseMenuId = "release_filter";
        public const string LoaderMenuId = "loader_filter";
        public const string UnsubscribeButtonId = "entry-unsub";
        public const string UnsubscribeConfirmButtonId = "entry-unsub-confirm";
        public const string UnsubscribeCancelButtonId = "entry-unsub-cancel";

        /// <summary>
        ///     Settings panel of a subscription, send it with <see cref="MessageFlags.ComponentsV2" />
        /// </summary>
        /// <param name="listPage">List page the panel was opened from</param>
        /// <param name="ownerId">The only user who can use the panel</param>
        /// <param name="notice">Short note under the title, e.g. "Subscribed to X"</param>
        public static async Task<MessageComponent> BuildSettingsPanelAsync(ModrinthEntry entry, ulong ownerId,
            int listPage, IDataService dataService, IModrinthClient modrinthClient, ILocalizationService loc,
            CultureInfo? culture, string? notice = null)
        {
            string Id(string prefix) => $"{prefix}:{ownerId}:{entry.ProjectId}:{listPage}";

            // Only needed for the loader options, the panel works without them
            Project? project = null;
            try
            {
                project = await modrinthClient.Project.GetAsync(entry.ProjectId);
            }
            catch (Exception)
            {
                // Modrinth API unavailable
            }

            var title = project?.Title ?? (await dataService.GetModrinthProjectByIdAsync(entry.ProjectId))?.Title ??
                entry.ProjectId;

            var fields = new List<SettingsField>
            {
                new(loc.Get("settings-channel", culture),
                    SettingsPanelBuilder.ChannelPicker(Id(ChannelMenuId), entry.CustomUpdateChannel,
                        loc.Get("settings-channel-placeholder", culture))),
                new(loc.Get("settings-ping-role", culture),
                    SettingsPanelBuilder.RolePicker(Id(PingRoleMenuId), entry.CustomPingRole,
                        loc.Get("settings-ping-role-placeholder", culture))),
                new(loc.Get("settings-release-filter", culture),
                    SettingsPanelBuilder.ReleaseFilterMenu(Id(ReleaseMenuId), entry.ReleaseFilter,
                        loc.Get("settings-release-placeholder", culture)))
            };

            var loaderMenu = SettingsPanelBuilder.LoaderFilterMenu(Id(LoaderMenuId), project?.Loaders ?? [],
                entry.LoaderFilter, loc.Get("settings-loader-placeholder", culture));
            if (loaderMenu is not null)
                fields.Add(new SettingsField(loc.Get("settings-loader-filter", culture), loaderMenu));

            var buttons = new[]
            {
                ManageInteractionModule.BackButton(ownerId, ManageTab.Projects, listPage, loc, culture),
                new ButtonBuilder(loc.Get("button-unsubscribe", culture), Id(UnsubscribeButtonId), ButtonStyle.Danger)
            };

            return SettingsPanelBuilder.Build(
                loc.Get("settings-subscription-title", culture, new { title, id = entry.ProjectId }),
                SettingsPanelBuilder.SubscriptionAccent, fields, buttons, notice);
        }

        [DoUserCheck]
        [ComponentInteraction(OpenButtonId + ":*:*:*")]
        public Task HandleOpenAsync(ulong ownerId, string projectId, int listPage)
        {
            return UpdateAsync(ownerId, projectId, listPage, _ => Task.CompletedTask);
        }

        [DoUserCheck]
        [ComponentInteraction(UnsubscribeButtonId + ":*:*:*")]
        public async Task HandleUnsubscribeAsync(ulong ownerId, string projectId, int listPage)
        {
            await DeferAsync();

            var entry = await dataService.GetModrinthEntryAsync(Context.Guild.Id, projectId);
            if (entry == null)
            {
                await ShowResultAsync(ownerId, listPage, LocalizationService.Get("entry-not-found", CommandCultureInfo));
                return;
            }

            var project = await dataService.GetModrinthProjectByIdAsync(projectId);
            var question = LocalizationService.Get("entry-unsub-confirm-question", CommandCultureInfo,
                new { title = project?.Title ?? projectId, id = projectId });

            await ModifyOriginalResponseAsync(msg => msg.Components = SettingsPanelBuilder.BuildMessage(question,
                SettingsPanelBuilder.SubscriptionAccent,
                new ButtonBuilder(LocalizationService.Get("button-confirm", CommandCultureInfo),
                    $"{UnsubscribeConfirmButtonId}:{ownerId}:{projectId}:{listPage}", ButtonStyle.Danger),
                new ButtonBuilder(LocalizationService.Get("button-cancel", CommandCultureInfo),
                    $"{UnsubscribeCancelButtonId}:{ownerId}:{projectId}:{listPage}", ButtonStyle.Secondary)));
        }

        [DoUserCheck]
        [ComponentInteraction(UnsubscribeConfirmButtonId + ":*:*:*")]
        public async Task HandleUnsubscribeConfirmAsync(ulong ownerId, string projectId, int listPage)
        {
            await DeferAsync();

            var project = await dataService.GetModrinthProjectByIdAsync(projectId);

            // Also removes the shared project state if nothing else uses it
            if (!await dataService.RemoveModrinthProjectFromGuildAsync(Context.Guild.Id, projectId))
            {
                await ShowResultAsync(ownerId, listPage, LocalizationService.Get("entry-not-found", CommandCultureInfo));
                return;
            }

            await ShowResultAsync(ownerId, listPage, LocalizationService.Get("entry-unsubscribed", CommandCultureInfo,
                new { title = project?.Title ?? projectId, id = projectId }));
        }

        [DoUserCheck]
        [ComponentInteraction(UnsubscribeCancelButtonId + ":*:*:*")]
        public Task HandleUnsubscribeCancelAsync(ulong ownerId, string projectId, int listPage)
        {
            return UpdateAsync(ownerId, projectId, listPage, _ => Task.CompletedTask);
        }

        [DoUserCheck]
        [ComponentInteraction(ChannelMenuId + ":*:*:*")]
        public async Task HandleChannelSelectionAsync(ulong ownerId, string projectId, int listPage, string[] selectedValues)
        {
            var channelId = SettingsPanelBuilder.ParseSelectedId(selectedValues);
            if (channelId is null)
                return;

            if (!BotCanSendIn(channelId.Value))
            {
                // Show the panel again, so the picker goes back to the saved channel
                await UpdateAsync(ownerId, projectId, listPage, _ => Task.CompletedTask);
                await FollowupAsync(LocalizationService.Get("follow-no-send-permission", CommandCultureInfo,
                    new { channel = MentionUtils.MentionChannel(channelId.Value) }), ephemeral: true);
                return;
            }

            await UpdateAsync(ownerId, projectId, listPage,
                _ => dataService.ChangeModrinthEntryChannel(Context.Guild.Id, projectId, channelId.Value));
        }

        [DoUserCheck]
        [ComponentInteraction(PingRoleMenuId + ":*:*:*")]
        public Task HandlePingRoleSelectionAsync(ulong ownerId, string projectId, int listPage, string[] selectedValues)
        {
            var roleId = SettingsPanelBuilder.ParseSelectedId(selectedValues);
            return UpdateAsync(ownerId, projectId, listPage,
                _ => dataService.SetPingRoleAsync(Context.Guild.Id, roleId, projectId));
        }

        [DoUserCheck]
        [ComponentInteraction(ReleaseMenuId + ":*:*:*")]
        public async Task HandleReleaseFilterSelectionAsync(ulong ownerId, string projectId, int listPage, string[] selectedValues)
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

            await UpdateAsync(ownerId, projectId, listPage,
                entry => dataService.SetReleaseFilterAsync(entry.EntryId, releaseFilter));
        }

        [DoUserCheck]
        [ComponentInteraction(LoaderMenuId + ":*:*:*")]
        public Task HandleLoaderFilterSelectionAsync(ulong ownerId, string projectId, int listPage, string[] selectedValues)
        {
            var loaderFilter = selectedValues.Length > 0 ? selectedValues : null;
            return UpdateAsync(ownerId, projectId, listPage,
                entry => dataService.SetLoaderFilterAsync(entry.EntryId, loaderFilter));
        }

        /// <summary>
        ///     Applies a change to the guild's entry of the project and shows the panel with the saved state
        /// </summary>
        private async Task UpdateAsync(ulong ownerId, string projectId, int listPage, Func<ModrinthEntry, Task> update)
        {
            await DeferAsync();

            var entry = await dataService.GetModrinthEntryAsync(Context.Guild.Id, projectId);
            if (entry == null)
            {
                await ShowResultAsync(ownerId, listPage, LocalizationService.Get("entry-not-found", CommandCultureInfo));
                return;
            }

            await update(entry);

            // Reload, the data service saves through its own context
            entry = await dataService.GetModrinthEntryAsync(Context.Guild.Id, projectId) ?? entry;

            var panel = await BuildSettingsPanelAsync(entry, ownerId, listPage, dataService, modrinthClient,
                LocalizationService, CommandCultureInfo);

            await ModifyOriginalResponseAsync(msg => msg.Components = panel);
        }

        /// <summary>
        ///     Goes back to the list with the text as a note
        /// </summary>
        private async Task ShowResultAsync(ulong ownerId, int listPage, string text)
        {
            var components = await ManageInteractionModule.BuildPanelAsync(dataService, followService,
                Context.Guild.Id, ownerId, ManageTab.Projects, listPage, CanManageServerHere, LocalizationService,
                CommandCultureInfo, text);

            await ModifyOriginalResponseAsync(msg => msg.Components = components);
        }
    }
}
