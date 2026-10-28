using System.Globalization;
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
using Modrinth;

namespace Asterion.Modules;

public enum ManageTab
{
    Projects,
    Creators,
    General,
    Notifications
}

/// <summary>
///     The <c>/manage</c> panel: tabs with the subscribed projects, the followed creators and the server settings.
///     The panel is ephemeral, the owner's ID (who ran the command) is still the first part of every custom ID
///     (<c>list:{owner}:{tab}:{page}:{action}</c>) for <see cref="DoUserCheck" />.
///     All state is in the custom IDs, so the buttons survive restarts.
///     The server tabs are only for administrators, everything else also for the manage role.
/// </summary>
[RequireContext(ContextType.Guild)]
[RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
[DoManageSubsRoleCheck(Group = "ManageSubs")]
// ReSharper disable once ClassNeverInstantiated.Global
public class ManageInteractionModule(
    ILocalizationService localizationService,
    IGuildCultureProvider cultureProvider,
    IGuildService guildService,
    ISubscriptionService subscriptionService,
    IFollowService followService,
    IModrinthClient modrinthClient,
    ModrinthService modrinthService)
    : AsterionInteractionModuleBase(localizationService, cultureProvider)
{
    // "list" as the panel started as the /list command, kept so its messages keep working
    public const string PanelButtonId = "list";
    public const string AddProjectButtonId = "manage-add-project";
    public const string AddFollowButtonId = "manage-add-follow";
    public const string AddProjectModalId = "manage-add-project-modal";
    public const string AddFollowModalId = "manage-add-follow-modal";

    private const string ProjectInputId = "project";
    private const string UsernameInputId = "username";
    private const string ChannelInputId = "channel";
    private const string NewProjectChannelInputId = "new_project_channel";
    private const string EventsInputId = "events";

    public static ButtonBuilder BackButton(ulong ownerId, ManageTab tab, int page, ILocalizationService loc,
        CultureInfo? culture)
    {
        return new ButtonBuilder(loc.Get("button-back", culture), PanelButtonCustomId(ownerId, tab, page, "back"),
            ButtonStyle.Secondary);
    }

    /// <summary>
    ///     Custom ID of a button that shows the tab's page of the panel
    /// </summary>
    public static string PanelButtonCustomId(ulong ownerId, ManageTab tab, int page, string action)
    {
        return $"{PanelButtonId}:{ownerId}:{TabId(tab)}:{page}:{action}";
    }

    /// <summary>
    ///     Builds the panel. Without a tab, it opens on projects, or on creators if there are no projects.
    ///     The server tabs fall back to projects for users who can't manage the server.
    /// </summary>
    /// <param name="ownerId">The only user who can use the panel's buttons</param>
    /// <param name="canManageServer">If the owner may open the server tabs (is an administrator)</param>
    /// <param name="notice">Short note under the header, e.g. "Stopped following X"</param>
    public static async Task<MessageComponent> BuildPanelAsync(ISubscriptionService subscriptionService,
        IGuildService guildService, IFollowService followService, ulong guildId, ulong ownerId, ManageTab? tab,
        int page, bool canManageServer, ILocalizationService loc, CultureInfo? culture, string? notice = null)
    {
        var entries = (await subscriptionService.GetSubscriptionsAsync(guildId))
            .OrderBy(e => e.Project.Title ?? e.ProjectId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.ProjectId)
            .ToList();
        // Follows with their projects are only loaded for the creators tab, the other tabs need just the count
        var followCount = await followService.CountFollowsAsync(guildId);

        var shownTab = tab ?? (entries.Count == 0 && followCount > 0 ? ManageTab.Creators : ManageTab.Projects);
        if (!canManageServer && shownTab is ManageTab.General or ManageTab.Notifications)
            shownTab = ManageTab.Projects;

        ButtonBuilder TabButton(ManageTab buttonTab, string label, bool enabled = true)
        {
            return new ButtonBuilder(label, PanelButtonCustomId(ownerId, buttonTab, 0, "tab"),
                buttonTab == shownTab ? ButtonStyle.Primary : ButtonStyle.Secondary,
                isDisabled: buttonTab == shownTab || !enabled);
        }

        var tabs = new[]
        {
            TabButton(ManageTab.Projects, loc.Get("list-tab-projects", culture, new { count = entries.Count })),
            TabButton(ManageTab.Creators, loc.Get("list-tab-creators", culture, new { count = followCount })),
            TabButton(ManageTab.General, loc.Get("manage-tab-general", culture), canManageServer),
            TabButton(ManageTab.Notifications, loc.Get("manage-tab-notifications", culture), canManageServer)
        };

        switch (shownTab)
        {
            case ManageTab.General or ManageTab.Notifications:
            {
                var guild = await guildService.GetGuildAsync(guildId);
                if (guild is null)
                    return SettingsPanelBuilder.BuildMessage(loc.Get("error-internal", culture),
                        ServerSettingsPanelBuilder.Accent);

                var container = shownTab == ManageTab.General
                    ? ServerSettingsPanelBuilder.BuildGeneral(guild, ownerId, entries.Count, followCount,
                        loc.AvailableLocales, loc, culture, notice)
                    : ServerSettingsPanelBuilder.BuildNotifications(guild.GuildSettings, ownerId, loc, culture,
                        notice);

                return new ComponentBuilderV2()
                    .AddComponent(new ActionRowBuilder().AddComponents(tabs))
                    .AddComponent(container)
                    .Build();
            }
            case ManageTab.Creators:
                return BuildPage(await followService.GetFollowsAsync(guildId), page, ownerId, ManageTab.Creators,
                    loc.Get("follow-list-title", culture),
                    loc.Get("follow-list-description", culture),
                    SettingsPanelBuilder.FollowAccent,
                    follow => FollowEmbedBuilder.FormatListEntry(follow, loc, culture),
                    (follow, shownPage) => $"{FollowComponentBuilder.OpenButtonId}:{ownerId}:{follow.Id}:{shownPage}",
                    new ButtonBuilder(loc.Get("button-follow-creator", culture), $"{AddFollowButtonId}:{ownerId}",
                        ButtonStyle.Success),
                    loc.Get("follow-list-empty", culture),
                    loc, culture, notice, tabs);
            default:
                return BuildPage(entries, page, ownerId, ManageTab.Projects,
                    loc.Get("list-subscriptions-title", culture),
                    loc.Get("list-subscriptions-description", culture),
                    SettingsPanelBuilder.SubscriptionAccent,
                    entry => FormatSubscriptionEntry(entry, loc, culture),
                    (entry, shownPage) =>
                        $"{EntryInteractionModule.OpenButtonId}:{ownerId}:{entry.ProjectId}:{shownPage}",
                    new ButtonBuilder(loc.Get("button-add-project", culture), $"{AddProjectButtonId}:{ownerId}",
                        ButtonStyle.Success),
                    loc.Get("list-subscriptions-empty", culture),
                    loc, culture, notice, tabs);
        }
    }

    [SlashCommand("manage", "Manage subscribed projects, followed creators and server settings")]
    public async Task ManageAsync()
    {
        await DeferAsync(true);

        var components = await BuildPanelAsync(subscriptionService, guildService, followService, Context.Guild.Id,
            Context.User.Id, null, 0, CanManageServerHere, LocalizationService, CommandCultureInfo);

        await FollowupAsync(components: components, ephemeral: true,
            flags: MessageFlags.ComponentsV2 | MessageFlags.Ephemeral);
    }

    [DoUserCheck]
    [ComponentInteraction(PanelButtonId + ":*:*:*:*")]
    public async Task HandlePanelAsync(ulong ownerId, string tab, int page, string _)
    {
        await DeferAsync();
        await ShowPanelAsync(ownerId, ParseTab(tab), page);
    }

    [DoUserCheck]
    [ComponentInteraction(AddProjectButtonId + ":*")]
    public Task HandleAddProjectAsync(ulong ownerId)
    {
        var modal = new ModalBuilder()
            .WithTitle(Loc("manage-add-project-title"))
            .WithCustomId(AddProjectModalId)
            .AddLabel(Loc("manage-add-project-input"), new TextInputBuilder()
                .WithCustomId(ProjectInputId)
                .WithStyle(TextInputStyle.Short)
                .WithPlaceholder(Loc("manage-add-project-placeholder"))
                .WithMaxLength(200)
                .WithRequired(true))
            .AddLabel(Loc("settings-channel"), ChannelInput());

        return RespondWithModalAsync(modal.Build());
    }

    [DoUserCheck]
    [ComponentInteraction(AddFollowButtonId + ":*")]
    public Task HandleAddFollowAsync(ulong ownerId)
    {
        var events = FollowComponentBuilder.AddEventOptions(
            new SelectMenuBuilder().WithCustomId(EventsInputId).WithMinValues(1), FollowEvents.All,
            LocalizationService, CommandCultureInfo);

        var modal = new ModalBuilder()
            .WithTitle(Loc("manage-add-follow-title"))
            .WithCustomId(AddFollowModalId)
            .AddLabel(Loc("manage-add-follow-input"), new TextInputBuilder()
                .WithCustomId(UsernameInputId)
                .WithStyle(TextInputStyle.Short)
                .WithPlaceholder(Loc("manage-add-follow-placeholder"))
                .WithMaxLength(200)
                .WithRequired(true))
            .AddLabel(Loc("settings-channel"), ChannelInput())
            .AddLabel(Loc("settings-new-project-channel"),
                SettingsPanelBuilder.ChannelPicker(NewProjectChannelInputId, null,
                    Loc("settings-new-project-channel-placeholder"), optional: true).WithRequired(false))
            .AddLabel(Loc("settings-events"), events);

        return RespondWithModalAsync(modal.Build());
    }

    [ModalInteraction(AddProjectModalId)]
    public async Task HandleAddProjectSubmitAsync(RawModal _)
    {
        await DeferAsync();

        var modal = (SocketModal)Context.Interaction;
        var query = Subscriptions.ParseProjectInput(modal.GetText(ProjectInputId) ?? string.Empty);
        var channelId = SettingsPanelBuilder.ParseSelectedId(modal.GetSelected(ChannelInputId));

        if (channelId is null || !BotCanSendIn(channelId.Value))
        {
            await ShowPanelAsync(Context.User.Id, ManageTab.Projects, 0, ChannelError(channelId));
            return;
        }

        var result = await Subscriptions.SubscribeAsync(modrinthService, subscriptionService, Context.Guild.Id, query,
            channelId.Value);

        var notice = result.Status switch
        {
            SubscribeStatus.Subscribed => Loc("manage-subscribed", new
            {
                title = Format.Sanitize(result.Project!.Title), channel = MentionUtils.MentionChannel(channelId.Value)
            }),
            SubscribeStatus.AlreadySubscribed => Loc("manage-already-subscribed",
                new { title = Format.Sanitize(result.Project!.Title) }),
            SubscribeStatus.ProjectNotFound => Errors.ProjectNotFound(query).Text(LocalizationService, CommandCultureInfo),
            _ => Loc("error-internal")
        };

        // Open the project's settings, so the new subscription can be set up right away
        var entry = result.Project is null
            ? null
            : await subscriptionService.GetSubscriptionAsync(Context.Guild.Id, result.Project.Id);
        if (entry is null)
        {
            await ShowPanelAsync(Context.User.Id, ManageTab.Projects, 0, notice);
            return;
        }

        var panel = await EntryInteractionModule.BuildSettingsPanelAsync(entry, Context.User.Id, 0, guildService,
            modrinthClient, LocalizationService, CommandCultureInfo, notice);
        await ModifyOriginalResponseAsync(msg => msg.Components = panel);
    }

    [ModalInteraction(AddFollowModalId)]
    public async Task HandleAddFollowSubmitAsync(RawModal _)
    {
        await DeferAsync();

        var modal = (SocketModal)Context.Interaction;
        var query = modal.GetText(UsernameInputId) ?? string.Empty;
        var channelId = SettingsPanelBuilder.ParseSelectedId(modal.GetSelected(ChannelInputId));
        var newProjectChannelId = SettingsPanelBuilder.ParseSelectedId(modal.GetSelected(NewProjectChannelInputId));

        var events = FollowEvents.None;
        foreach (var value in modal.GetSelected(EventsInputId))
            if (int.TryParse(value, out var flag))
                events |= (FollowEvents)flag;

        if (channelId is null || !BotCanSendIn(channelId.Value))
        {
            await ShowPanelAsync(Context.User.Id, ManageTab.Creators, 0, ChannelError(channelId));
            return;
        }

        if (newProjectChannelId is not null && !BotCanSendIn(newProjectChannelId.Value))
        {
            await ShowPanelAsync(Context.User.Id, ManageTab.Creators, 0, ChannelError(newProjectChannelId));
            return;
        }

        var user = Subscriptions.ParseUserInput(query);
        if (user is null)
        {
            await ShowPanelAsync(Context.User.Id, ManageTab.Creators, 0,
                Errors.InputIsProject.Text(LocalizationService, CommandCultureInfo));
            return;
        }

        var (search, result) = await Subscriptions.FollowUserAsync(modrinthService, followService, Context.Guild.Id,
            user, channelId.Value, events == FollowEvents.None ? FollowEvents.ProjectUpdate : events,
            newProjectChannelId);
        if (result is null)
        {
            await ShowPanelAsync(Context.User.Id, ManageTab.Creators, 0,
                Errors.FromSearch(search).Text(LocalizationService, CommandCultureInfo));
            return;
        }

        var notice = FollowEmbedBuilder.DescribeAddResult(result, search.Payload.User.Username,
            MentionUtils.MentionChannel(channelId.Value), LocalizationService, CommandCultureInfo);

        // Open the follow's settings, so the new follow can be set up right away
        if (result.Follow is null)
        {
            await ShowPanelAsync(Context.User.Id, ManageTab.Creators, 0, notice);
            return;
        }

        var follow = await followService.GetFollowAsync(Context.Guild.Id, result.Follow.Id) ?? result.Follow;
        var panel = await FollowInteractionModule.BuildSettingsPanelAsync(followService, guildService, follow,
            Context.User.Id, 0, LocalizationService, CommandCultureInfo, notice);
        await ModifyOriginalResponseAsync(msg => msg.Components = panel);
    }

    private async Task ShowPanelAsync(ulong ownerId, ManageTab tab, int page, string? notice = null)
    {
        var components = await BuildPanelAsync(subscriptionService, guildService, followService, Context.Guild.Id,
            ownerId, tab, page, CanManageServerHere, LocalizationService, CommandCultureInfo, notice);

        await ModifyOriginalResponseAsync(msg => msg.Components = components);
    }

    /// <summary>
    ///     Channel picker for the modals, with the channel of the command preselected
    /// </summary>
    private SelectMenuBuilder ChannelInput()
    {
        ulong? current = Context.Channel is ITextChannel and not IThreadChannel ? Context.Channel.Id : null;

        return SettingsPanelBuilder.ChannelPicker(ChannelInputId, current, Loc("settings-channel-placeholder"));
    }

    private string ChannelError(ulong? channelId)
    {
        var card = channelId is null
            ? Errors.InvalidChannel
            : Warnings.BotCannotSend(MentionUtils.MentionChannel(channelId.Value));

        return card.Text(LocalizationService, CommandCultureInfo);
    }

    private string Loc(string key)
    {
        return LocalizationService.Get(key, CommandCultureInfo);
    }

    private string Loc(string key, object args)
    {
        return LocalizationService.Get(key, CommandCultureInfo, args);
    }

    private static MessageComponent BuildPage<T>(IList<T> items, int page, ulong ownerId, ManageTab tab,
        string title, string description, Color accent, Func<T, string> rowText,
        Func<T, int, string> settingsButtonId, ButtonBuilder addButton, string emptyText, ILocalizationService loc,
        CultureInfo? culture, string? notice, IEnumerable<ButtonBuilder> tabs)
    {
        // Clamp here too, the settings buttons need the page actually shown to come back to it
        var shownPage = Math.Clamp(page, 0, ListPageBuilder.GetPageCount(items.Count) - 1);

        return ListPageBuilder.Build(items, shownPage, title, description, accent,
            $"{PanelButtonId}:{ownerId}:{TabId(tab)}",
            rowText,
            item => new ButtonBuilder(loc.Get("button-settings", culture), settingsButtonId(item, shownPage),
                ButtonStyle.Secondary),
            emptyText, loc, culture, notice, [addButton], tabs).Components;
    }

    private static string FormatSubscriptionEntry(ModrinthEntry entry, ILocalizationService loc, CultureInfo? culture)
    {
        return loc.Get("list-subscription-entry", culture, new
        {
            title = Format.Sanitize(entry.Project.Title ?? entry.ProjectId),
            id = entry.ProjectId,
            channel = entry.CustomUpdateChannel.HasValue
                ? MentionUtils.MentionChannel(entry.CustomUpdateChannel.Value)
                : loc.Get("settings-none", culture),
            since = TimestampTag.FromDateTime(entry.Created, TimestampTagStyles.ShortDate).ToString()
        });
    }

    private static string TabId(ManageTab tab)
    {
        return tab switch
        {
            ManageTab.Creators => "c",
            ManageTab.General => "g",
            ManageTab.Notifications => "n",
            _ => "p"
        };
    }

    private static ManageTab ParseTab(string tabId)
    {
        return tabId switch
        {
            "c" => ManageTab.Creators,
            "g" => ManageTab.General,
            "n" => ManageTab.Notifications,
            _ => ManageTab.Projects
        };
    }
}
