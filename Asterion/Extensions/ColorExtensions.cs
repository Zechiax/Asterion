using Color = Discord.Color;

namespace Asterion.Extensions;

public static class ColorExtensions
{
    public static Discord.Color ToDiscordColor(this System.Drawing.Color color)
    {
        return new Color(color.R, color.G, color.B);
    }
}