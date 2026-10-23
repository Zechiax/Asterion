using System.Globalization;
using Asterion.Interfaces;
using Discord;
using Color = Discord.Color;

namespace Asterion.Common;

public enum StatusKind
{
    Error,
    Warning
}

/// <summary>
///     A localized error or warning, sent as a Components V2 card or shown as a panel's notice.
///     Get them from <see cref="Errors" /> and <see cref="Warnings" />.
/// </summary>
/// <param name="Key">Key of the message in errors.ftl</param>
/// <param name="TitleKey">Key of the title above the message, no title if null</param>
public sealed record StatusCard(StatusKind Kind, string Key, object? Args = null, string? TitleKey = null)
{
    public static readonly Color ErrorAccent = Color.Red;
    public static readonly Color WarningAccent = Color.Orange;

    /// <summary>
    ///     The message alone, e.g. for the notice of a panel
    /// </summary>
    public string Text(ILocalizationService loc, CultureInfo? culture)
    {
        return Args is null ? loc.Get(Key, culture) : loc.Get(Key, culture, Args);
    }

    public MessageComponent Build(ILocalizationService loc, CultureInfo? culture)
    {
        var text = Text(loc, culture);
        if (TitleKey is not null)
            text = $"### {loc.Get(TitleKey, culture)}\n{text}";

        var container = new ContainerBuilder()
            .WithAccentColor(Kind == StatusKind.Error ? ErrorAccent : WarningAccent)
            .AddComponent(new TextDisplayBuilder(text));

        return new ComponentBuilderV2().AddComponent(container).Build();
    }
}
