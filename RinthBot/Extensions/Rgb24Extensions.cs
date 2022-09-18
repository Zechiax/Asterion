using Discord;
using SixLabors.ImageSharp.PixelFormats;

namespace RinthBot.Extensions;

public static class Rgb24Extensions
{
    public static Discord.Color ToColor(this Rgb24 color)
    {
        return new Color(color.R, color.G, color.B);
    }
}