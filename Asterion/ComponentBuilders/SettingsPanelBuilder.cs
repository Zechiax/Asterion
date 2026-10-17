using Asterion.Database.Models;
using Asterion.EmbedBuilders;
using Discord;

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

    // Discord limit of options in one select menu
    private const int MaxSelectOptions = 25;

    /// <param name="buttons">Buttons under the fields, e.g. back and unfollow</param>
    public static MessageComponent Build(string title, Color accent, IEnumerable<SettingsField> fields,
        IEnumerable<ButtonBuilder> buttons)
    {
        var container = new ContainerBuilder()
            .WithAccentColor(accent)
            .AddComponent(new TextDisplayBuilder($"## {title}"));

        foreach (var field in fields)
            container
                .AddComponent(new TextDisplayBuilder($"**{field.Label}**"))
                .AddComponent(new ActionRowBuilder().AddComponent(field.Menu));

        container.AddComponent(new ActionRowBuilder().AddComponents(buttons.ToArray()));

        return new ComponentBuilderV2().AddComponent(container).Build();
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

    public static SelectMenuBuilder ChannelPicker(string customId, ulong? current, string placeholder)
    {
        var menu = new SelectMenuBuilder()
            .WithType(ComponentType.ChannelSelect)
            .WithCustomId(customId)
            .WithPlaceholder(placeholder)
            .WithMinValues(1)
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

    public static SelectMenuBuilder ReleaseFilterMenu(string customId, ReleaseType current, string placeholder)
    {
        return new SelectMenuBuilder()
            .WithCustomId(customId)
            .WithPlaceholder(placeholder)
            .WithMinValues(1)
            .WithMaxValues(3)
            .AddOption("Alpha", ((int)ReleaseType.Alpha).ToString(), isDefault: current.HasFlag(ReleaseType.Alpha))
            .AddOption("Beta", ((int)ReleaseType.Beta).ToString(), isDefault: current.HasFlag(ReleaseType.Beta))
            .AddOption("Release", ((int)ReleaseType.Release).ToString(),
                isDefault: current.HasFlag(ReleaseType.Release));
    }

    /// <summary>
    ///     Loader filter menu, null if there are no loaders to offer
    /// </summary>
    public static SelectMenuBuilder? LoaderFilterMenu(string customId, IEnumerable<string> loaders,
        string[]? current, string placeholder)
    {
        // Loaders of the current filter are kept first, so they stay selectable even past the option limit
        // or when no project uses them anymore; saving the menu would drop them otherwise
        var selected = (current ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var options = selected
            .Concat(loaders.Except(selected, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
            .Take(MaxSelectOptions)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (options.Length == 0)
            return null;

        var menu = new SelectMenuBuilder()
            .WithCustomId(customId)
            .WithPlaceholder(placeholder)
            .WithMinValues(0)
            .WithMaxValues(options.Length);

        foreach (var loader in options)
            menu.AddOption(loader, loader, isDefault: selected.Contains(loader, StringComparer.OrdinalIgnoreCase));

        return menu;
    }

    /// <summary>
    ///     Parses the ID selected in a channel or role picker, null if nothing was selected
    /// </summary>
    public static ulong? ParseSelectedId(string[] selectedValues)
    {
        return selectedValues.Length > 0 && ulong.TryParse(selectedValues[0], out var id) ? id : null;
    }
}
