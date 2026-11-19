using System.Globalization;
using Asterion.Database.Models;
using Asterion.EmbedBuilders;
using Asterion.Interfaces;
using Discord;
using Humanizer;

namespace Asterion.ComponentBuilders;

public sealed record SettingsField(string Label, SelectMenuBuilder Menu);

/// <summary>
///     Settings panel shared by subscriptions and follows, as a Components V2 message.
///     Each field gets a label, as a picker with a value hides its placeholder.
/// </summary>
public static class SettingsPanelBuilder
{
    public static readonly Color SubscriptionAccent = ModrinthEmbedBuilder.ModrinthColor;
    public static readonly Color FollowAccent = Color.Blue;

    /// <summary>
    ///     Sentinel option value in per-project/follow loader menus that explicitly allows all loaders,
    ///     ignoring the server's default loader filter.
    /// </summary>
    public const string AllLoadersOptionValue = "__all__";

    // Discord limit of options in one select menu
    private const int MaxSelectOptions = 25;
    private const int MaxSummaryLoadersLength = 60;

    /// <summary>
    ///     Most common Modrinth loaders, shown at the top of loader menus in this order before other loaders
    /// </summary>
    public static readonly string[] MainLoaders =
    [
        "fabric", "neoforge", "forge", "quilt",
        "paper", "purpur", "spigot", "velocity",
        "datapack", "iris", "optifine"
    ];

    /// <param name="buttons">Buttons under the fields, e.g. back and unfollow</param>
    /// <param name="notice">Short note under the title, e.g. the result of an action</param>
    public static MessageComponent Build(string title, Color accent, IEnumerable<SettingsField> fields,
        IEnumerable<ButtonBuilder> buttons, string? notice = null)
    {
        return new ComponentBuilderV2().AddComponent(BuildContainer(title, accent, fields, buttons, notice)).Build();
    }

    /// <summary>
    ///     The panel's container, for panels with more components around it (e.g. the manage tabs)
    /// </summary>
    public static ContainerBuilder BuildContainer(string title, Color accent, IEnumerable<SettingsField> fields,
        IEnumerable<ButtonBuilder> buttons, string? notice = null)
    {
        var header = $"## {title}";
        if (!string.IsNullOrEmpty(notice))
            header += "\n" + notice;

        var container = new ContainerBuilder()
            .WithAccentColor(accent)
            .AddComponent(new TextDisplayBuilder(header));

        foreach (var field in fields)
            AddField(container, field);

        var buttonArray = buttons.ToArray();
        if (buttonArray.Length > 0)
            container.AddComponent(new ActionRowBuilder().AddComponents(buttonArray));

        return container;
    }

    /// <summary>
    ///     Adds the field's label and menu to the container
    /// </summary>
    public static ContainerBuilder AddField(ContainerBuilder container, SettingsField field)
    {
        return container
            .AddComponent(new TextDisplayBuilder($"**{field.Label}**"))
            .AddComponent(new ActionRowBuilder().AddComponent(field.Menu));
    }

    /// <summary>
    ///     Adds a setting that is changed with a button: its label and current value, with the button next to it
    /// </summary>
    public static ContainerBuilder AddButtonSetting(ContainerBuilder container, string label, string value,
        ButtonBuilder button)
    {
        return container.AddComponent(new SectionBuilder()
            .AddComponent(new TextDisplayBuilder($"**{label}**\n-# {value}"))
            .WithAccessory(button));
    }

    /// <summary>
    ///     A text in the panel's style, for the confirm, done and not found states of a panel
    /// </summary>
    public static MessageComponent BuildMessage(string text, Color accent, params ButtonBuilder[] buttons)
    {
        var container = new ContainerBuilder()
            .WithAccentColor(accent)
            .AddComponent(new TextDisplayBuilder(text));

        if (buttons.Length > 0)
            container.AddComponent(new ActionRowBuilder().AddComponents(buttons));

        return new ComponentBuilderV2().AddComponent(container).Build();
    }

    /// <param name="optional">If the channel can be deselected, e.g. to fall back to another channel</param>
    public static SelectMenuBuilder ChannelPicker(string customId, ulong? current, string placeholder,
        bool optional = false)
    {
        var menu = new SelectMenuBuilder()
            .WithType(ComponentType.ChannelSelect)
            .WithCustomId(customId)
            .WithPlaceholder(placeholder)
            .WithMinValues(optional ? 0 : 1)
            .WithMaxValues(1)
            .WithChannelTypes(ChannelType.Text, ChannelType.News);

        if (current.HasValue)
            menu.AddDefaultValue(current.Value, SelectDefaultValueType.Channel);

        return menu;
    }

    /// <summary>
    ///     Role picker, deselecting the role removes it
    /// </summary>
    public static SelectMenuBuilder RolePicker(string customId, ulong? current, string placeholder)
    {
        var menu = new SelectMenuBuilder()
            .WithType(ComponentType.RoleSelect)
            .WithCustomId(customId)
            .WithPlaceholder(placeholder)
            .WithMinValues(0)
            .WithMaxValues(1);

        if (current.HasValue)
            menu.AddDefaultValue(current.Value, SelectDefaultValueType.Role);

        return menu;
    }

    /// <param name="optional">If the filter can be deselected to inherit the server default</param>
    public static SelectMenuBuilder ReleaseFilterMenu(string customId, ReleaseType? current, string placeholder,
        bool optional = false)
    {
        return new SelectMenuBuilder()
            .WithCustomId(customId)
            .WithPlaceholder(placeholder)
            .WithMinValues(optional ? 0 : 1)
            .WithMaxValues(3)
            .AddOption("Alpha", ((int)ReleaseType.Alpha).ToString(),
                isDefault: current is { } alpha && alpha.HasFlag(ReleaseType.Alpha))
            .AddOption("Beta", ((int)ReleaseType.Beta).ToString(),
                isDefault: current is { } beta && beta.HasFlag(ReleaseType.Beta))
            .AddOption("Release", ((int)ReleaseType.Release).ToString(),
                isDefault: current is { } release && release.HasFlag(ReleaseType.Release));
    }

    /// <summary>
    ///     Loader filter menu, null if there are no loaders to offer
    /// </summary>
    /// <param name="allLoadersOptionLabel">
    ///     When set, prepends an option with value <see cref="AllLoadersOptionValue" /> that overrides the server's
    ///     default loader filter to allow all loaders (selected when <paramref name="current" /> is an empty array).
    /// </param>
    /// <param name="preserveLoaderOrder">
    ///     When true, keeps the caller's priority order of <paramref name="loaders" /> when trimming to Discord's
    ///     25-option cap, then sorts the kept options alphabetically for display.
    /// </param>
    public static SelectMenuBuilder? LoaderFilterMenu(string customId, IEnumerable<string> loaders,
        string[]? current, string placeholder, string? allLoadersOptionLabel = null, bool preserveLoaderOrder = false)
    {
        var hasAllOverride = !string.IsNullOrEmpty(allLoadersOptionLabel);
        var maxLoaderOptions = hasAllOverride ? MaxSelectOptions - 1 : MaxSelectOptions;

        // Loaders of the current filter are kept first, so they stay selectable even past the option limit
        // or when no project uses them anymore; saving the menu would drop them otherwise
        var selected = (current ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var remaining = loaders.Except(selected, StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        if (!preserveLoaderOrder)
            remaining = remaining.Order(StringComparer.OrdinalIgnoreCase);

        var options = selected
            .Concat(remaining)
            .Take(maxLoaderOptions)
            .OrderBy(LoaderSortRank)
            .ThenBy(l => l, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (options.Length == 0 && !hasAllOverride)
            return null;

        var totalOptions = options.Length + (hasAllOverride ? 1 : 0);
        var menu = new SelectMenuBuilder()
            .WithCustomId(customId)
            .WithPlaceholder(placeholder)
            .WithMinValues(0)
            .WithMaxValues(totalOptions);

        if (hasAllOverride)
            menu.AddOption(allLoadersOptionLabel!, AllLoadersOptionValue, isDefault: current is { Length: 0 });

        foreach (var loader in options)
            menu.AddOption(loader, loader, isDefault: selected.Contains(loader, StringComparer.OrdinalIgnoreCase));

        return menu;
    }

    /// <summary>
    ///     Parses the release types selected in a menu, null if nothing was selected (inherit server default)
    /// </summary>
    public static ReleaseType? ParseReleaseFilter(string[] selectedValues)
    {
        ReleaseType filter = 0;
        foreach (var value in selectedValues)
            if (int.TryParse(value, out var flag))
                filter |= (ReleaseType)flag;

        return filter == 0 ? null : filter;
    }

    /// <summary>
    ///     Parses the loaders selected in a per-project/follow menu:
    ///     null = inherit server default, empty array = explicitly allow all loaders, non-empty = specific loaders.
    /// </summary>
    public static string[]? ParseLoaderFilter(string[] selectedValues)
    {
        if (selectedValues.Length == 0)
            return null;

        if (selectedValues.Contains(AllLoadersOptionValue, StringComparer.Ordinal))
            return [];

        return selectedValues;
    }

    public static string FormatReleaseFilter(ReleaseType filter)
    {
        var parts = new List<string>(3);
        if (filter.HasFlag(ReleaseType.Release))
            parts.Add("Release");
        if (filter.HasFlag(ReleaseType.Beta))
            parts.Add("Beta");
        if (filter.HasFlag(ReleaseType.Alpha))
            parts.Add("Alpha");

        return string.Join(", ", parts);
    }

    public static string FormatLoaderFilter(string[]? loaders, ILocalizationService loc, CultureInfo? culture)
    {
        if (loaders is not { Length: > 0 })
            return loc.Get("settings-loader-placeholder", culture);

        return string.Join(", ", loaders).Truncate(MaxSummaryLoadersLength);
    }

    public static string ReleaseFilterPlaceholder(GuildSettings? guildSettings, ILocalizationService loc,
        CultureInfo? culture)
    {
        var value = FormatReleaseFilter(guildSettings?.DefaultReleaseFilter ?? GuildSettings.AllReleaseTypes);
        return loc.Get("settings-release-placeholder-default", culture, new { value });
    }

    public static string LoaderFilterPlaceholder(GuildSettings? guildSettings, ILocalizationService loc,
        CultureInfo? culture)
    {
        if (guildSettings?.DefaultLoaderFilter is { Length: > 0 } defaults)
            return loc.Get("settings-loader-placeholder-default", culture,
                new { value = FormatLoaderFilter(defaults, loc, culture) });

        return loc.Get("settings-loader-placeholder", culture);
    }

    public static string? AllLoadersOverrideLabel(GuildSettings? guildSettings, ILocalizationService loc,
        CultureInfo? culture)
    {
        return guildSettings?.DefaultLoaderFilter is { Length: > 0 }
            ? loc.Get("settings-loader-all-override", culture)
            : null;
    }

    private static int LoaderSortRank(string loader)
    {
        var index = Array.FindIndex(MainLoaders, m => string.Equals(m, loader, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : MainLoaders.Length;
    }

    /// <summary>
    ///     Parses the ID selected in a channel or role picker, null if nothing was selected
    /// </summary>
    public static ulong? ParseSelectedId(string[] selectedValues)
    {
        return selectedValues.Length > 0 && ulong.TryParse(selectedValues[0], out var id) ? id : null;
    }
}
