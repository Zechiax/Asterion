using System.Globalization;
using Asterion.Attributes;
using Asterion.Common;
using Asterion.ComponentBuilders;
using Asterion.Database.Models;
using Asterion.Interfaces;
using Discord;
using Discord.Interactions;

namespace Asterion.Modules;

public enum ListTab
{
    Projects
}

/// <summary>
///     The <c>/list</c> message: one page of subscribed projects.
///     The message is public but only its owner (who ran the command) can use it, the owner's ID is the first
///     part of every custom ID (<c>list:{owner}:{tab}:{page}:{action}</c>) for <see cref="DoUserCheck" />.
///     All state is in the custom IDs, so the buttons survive restarts.
/// </summary>
[RequireContext(ContextType.Guild)]
[RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
[DoManageSubsRoleCheck(Group = "ManageSubs")]
[DoUserCheck]
// ReSharper disable once ClassNeverInstantiated.Global
public class ListInteractionModule(
    ILocalizationService localizationService,
    IGuildCultureProvider cultureProvider,
    IDataService dataService)
    : AsterionInteractionModuleBase(localizationService, cultureProvider)
{
    public const string ListButtonId = "list";

    // Page of a settings panel that was not opened from the list, such a panel has no back button
    public const int NoList = -1;

    public static ButtonBuilder BackButton(ulong ownerId, ListTab tab, int page, ILocalizationService loc,
        CultureInfo? culture)
    {
        return new ButtonBuilder(loc.Get("button-back", culture), PageButtonId(ownerId, tab, page, "back"),
            ButtonStyle.Secondary);
    }

    /// <param name="ownerId">The only user who can use the list's buttons</param>
    /// <param name="notice">Short note under the header, e.g. "Unsubscribed from X"</param>
    /// <returns>The components, and whether the guild has no subscriptions</returns>
    public static async Task<(MessageComponent Components, bool IsEmpty)> BuildListAsync(IDataService dataService,
        ulong guildId, ulong ownerId, ListTab? tab, int page, ILocalizationService loc, CultureInfo? culture,
        string? notice = null)
    {
        var entries = ((await dataService.GetAllGuildsSubscribedProjectsAsync(guildId)) ?? [])
            .OrderBy(e => e.Project.Title ?? e.ProjectId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.ProjectId)
            .ToList();

        var components = BuildPage(entries, page, ownerId, ListTab.Projects,
            loc.Get("list-subscriptions-title", culture),
            loc.Get("list-subscriptions-description", culture),
            SettingsPanelBuilder.SubscriptionAccent,
            entry => FormatSubscriptionEntry(entry, loc, culture),
            (entry, shownPage) =>
                $"{EntryInteractionModule.OpenButtonId}:{ownerId}:{entry.ProjectId}:{shownPage}",
            loc.Get("list-subscriptions-empty", culture),
            loc, culture, notice);

        return (components, entries.Count == 0);
    }

    [ComponentInteraction(ListButtonId + ":*:*:*:*")]
    public async Task HandleListAsync(ulong ownerId, string tab, int page, string _)
    {
        await DeferAsync();

        var (components, _) = await BuildListAsync(dataService, Context.Guild.Id, ownerId, ParseTab(tab), page,
            LocalizationService, CommandCultureInfo);

        await ModifyOriginalResponseAsync(msg => msg.Components = components);
    }

    private static MessageComponent BuildPage<T>(IList<T> items, int page, ulong ownerId, ListTab tab, string title,
        string description, Color accent, Func<T, string> rowText, Func<T, int, string> settingsButtonId,
        string emptyText, ILocalizationService loc, CultureInfo? culture, string? notice)
    {
        // Clamp here too, the settings buttons need the page actually shown to come back to it
        var shownPage = Math.Clamp(page, 0, ListPageBuilder.GetPageCount(items.Count) - 1);

        return ListPageBuilder.Build(items, shownPage, title, description, accent,
            $"{ListButtonId}:{ownerId}:{TabId(tab)}",
            rowText,
            item => new ButtonBuilder(loc.Get("button-settings", culture), settingsButtonId(item, shownPage),
                ButtonStyle.Secondary),
            emptyText, loc, culture, notice).Components;
    }

    private static string FormatSubscriptionEntry(ModrinthEntry entry, ILocalizationService loc, CultureInfo? culture)
    {
        return loc.Get("list-subscription-entry", culture, new
        {
            title = entry.Project.Title ?? entry.ProjectId,
            id = entry.ProjectId,
            channel = entry.CustomUpdateChannel.HasValue
                ? MentionUtils.MentionChannel(entry.CustomUpdateChannel.Value)
                : loc.Get("settings-none", culture),
            since = TimestampTag.FromDateTime(entry.Created, TimestampTagStyles.ShortDate).ToString()
        });
    }

    private static string PageButtonId(ulong ownerId, ListTab tab, int page, string action)
    {
        return $"{ListButtonId}:{ownerId}:{TabId(tab)}:{page}:{action}";
    }

    private static string TabId(ListTab tab)
    {
        return "p";
    }

    private static ListTab ParseTab(string tabId)
    {
        return ListTab.Projects;
    }
}
