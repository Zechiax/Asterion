using AsterionNg.Assets;
using Discord;

namespace AsterionNg.Common.EmbedStyles;

/// <summary>
/// Represents the style of an unsuccessful embed.
/// </summary>
public class UnsuccessfulEmbedStyle : EmbedStyle
{
    /// <inheritdoc/>
    public override string Name => "Woops!";

    /// <inheritdoc/>
    public override string IconUrl => Icons.Cross;

    /// <inheritdoc/>
    public override Color Color => Colors.Danger;
}
