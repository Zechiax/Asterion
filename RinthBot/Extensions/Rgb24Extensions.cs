using System.Drawing;
using SixLabors.ImageSharp.PixelFormats;
using Color = Discord.Color;

namespace RinthBot.Extensions;

public static class Rgb24Extensions
{
    public static Discord.Color ToDiscordColor(this Rgb24 color)
    {
        return new Color(color.R, color.G, color.B);
    }
}