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
        // Keep loaders of the current filter selectable even if no project uses them anymore
        var options = loaders
            .Concat(current ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Take(MaxSelectOptions)
            .ToArray();

        if (options.Length == 0)
            return null;

        var menu = new SelectMenuBuilder()
            .WithCustomId(customId)
            .WithPlaceholder(placeholder)
            .WithMinValues(0)
            .WithMaxValues(options.Length);

        foreach (var loader in options)
            menu.AddOption(loader, loader, isDefault: current != null && current.Contains(loader));

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
