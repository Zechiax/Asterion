using SixLabors.ImageSharp.PixelFormats;
using Color = Discord.Color;

namespace Asterion.Extensions;

public static class Rgb24Extensions
{
    public static Color ToDiscordColor(this Rgb24 color)
    {
        return new Color(color.R, color.G, color.B);
    }
}