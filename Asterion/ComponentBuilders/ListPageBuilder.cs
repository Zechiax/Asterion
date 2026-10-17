using System.Globalization;
using Asterion.Interfaces;
using Discord;
using Humanizer;

namespace Asterion.ComponentBuilders;

/// <summary>
///     Builds one page of a list as a Components V2 message: an optional row of tabs, a container with a header and
///     one section per row, each with a button next to it, followed by previous/refresh/next buttons.
///     Pagination is stateless: the target page is in the button's custom ID (<c>{prefix}:{page}:{action}</c>)
///     and the handler reloads the list, so the buttons never time out and survive restarts.
/// </summary>
public static class ListPageBuilder
{
    /// <summary>
    ///     Rows per page. Discord allows 40 components per message (nested ones included); a full page uses
    ///     3 per row (section, text, button) plus 3 (container, header, separator), at most 6 for the navigation
    ///     row (action row, 5 buttons) and at most 5 for the tabs row (action row, 4 tabs).
    /// </summary>
    public const int RowsPerPage = 8;

    /// <summary>
    ///     Characters per row, Discord allows 4000 characters of text in all text displays of a message together
    /// </summary>
    private const int MaxRowTextLength = 350;

    public const string PreviousAction = "prev";
    public const string RefreshAction = "refresh";
    public const string NextAction = "next";

    public static int GetPageCount(int count)
    {
        return Math.Max(1, (count + RowsPerPage - 1) / RowsPerPage);
    }

    /// <summary>
    ///     Builds one page. The page is clamped to the valid range, as rows can be added or removed between clicks.
    ///     Send the result with <see cref="MessageFlags.ComponentsV2" />.
    /// </summary>
    /// <param name="page">Zero-based page index</param>
    /// <param name="navIdPrefix">Custom ID prefix of the previous/refresh/next buttons</param>
    /// <param name="rowText">Markdown text of a row</param>
    /// <param name="rowButton">Button shown next to a row, its custom ID must be unique per row</param>
    /// <param name="emptyText">Text shown when there are no rows</param>
    /// <param name="notice">Short note under the header, e.g. the result of an action</param>
    /// <param name="extraButtons">Buttons after the navigation, at most 2 so the row has at most 5</param>
    /// <param name="tabs">Buttons in a row above the list, at most 4</param>
    /// <returns>The components and the page actually shown</returns>
    public static (MessageComponent Components, int Page) Build<T>(IList<T> items, int page, string title,
        string description, Color accent, string navIdPrefix, Func<T, string> rowText, Func<T, ButtonBuilder> rowButton,
        string emptyText, ILocalizationService loc, CultureInfo? culture, string? notice = null,
        IEnumerable<ButtonBuilder>? extraButtons = null, IEnumerable<ButtonBuilder>? tabs = null)
    {
        var pageCount = GetPageCount(items.Count);
        page = Math.Clamp(page, 0, pageCount - 1);

        var header = $"## {title}\n{description}";
        if (pageCount > 1)
            header += "\n-# " + loc.Get("list-page", culture, new { page = page + 1, count = pageCount });
        if (!string.IsNullOrEmpty(notice))
            header += "\n" + notice;

        var container = new ContainerBuilder()
            .WithAccentColor(accent)
            .AddComponent(new TextDisplayBuilder(header))
            .AddComponent(new SeparatorBuilder());

        if (items.Count == 0)
            container.AddComponent(new TextDisplayBuilder(emptyText));

        foreach (var item in items.Skip(page * RowsPerPage).Take(RowsPerPage))
            container.AddComponent(new SectionBuilder()
                .AddComponent(new TextDisplayBuilder(rowText(item).Truncate(MaxRowTextLength)))
                .WithAccessory(rowButton(item)));

        var navigation = new ActionRowBuilder();
        if (pageCount > 1)
            navigation.AddComponent(new ButtonBuilder("◀", $"{navIdPrefix}:{Math.Max(page - 1, 0)}:{PreviousAction}",
                ButtonStyle.Secondary, isDisabled: page <= 0));

        navigation.AddComponent(new ButtonBuilder(loc.Get("button-refresh", culture),
            $"{navIdPrefix}:{page}:{RefreshAction}", ButtonStyle.Secondary));

        if (pageCount > 1)
            navigation.AddComponent(new ButtonBuilder("▶",
                $"{navIdPrefix}:{Math.Min(page + 1, pageCount - 1)}:{NextAction}", ButtonStyle.Secondary,
                isDisabled: page >= pageCount - 1));

        foreach (var button in extraButtons ?? [])
            navigation.AddComponent(button);

        var builder = new ComponentBuilderV2();

        var tabButtons = tabs?.ToArray() ?? [];
        if (tabButtons.Length > 0)
            builder.AddComponent(new ActionRowBuilder().AddComponents(tabButtons));

        var components = builder
            .AddComponent(container)
            .AddComponent(navigation)
            .Build();

        return (components, page);
    }
}
