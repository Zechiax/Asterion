using SkiaSharp;
using Color = Discord.Color;

namespace Asterion.Services;

public static class ImageService
{
    private const int SampleSize = 100;

    public static async Task<Color> GetMajorColorFromImageUrl(this HttpClient client, string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return new Color();

        try
        {
            await using var stream = await client.GetStreamAsync(imageUrl);
            using var original = SKBitmap.Decode(stream);
            if (original is null) return new Color();

            // Scale down preserving the aspect ratio to speed up the averaging.
            var height = Math.Max(1, original.Height * SampleSize / Math.Max(1, original.Width));
            using var bitmap = original.Resize(new SKImageInfo(SampleSize, height, SKColorType.Rgba8888, SKAlphaType.Unpremul),
                SKFilterQuality.Low);
            if (bitmap is null) return new Color();

            long r = 0, g = 0, b = 0, weight = 0;
            foreach (var pixel in bitmap.Pixels)
            {
                // Ignore transparent pixels, weight the rest by opacity.
                if (pixel.Alpha == 0) continue;
                r += pixel.Red * pixel.Alpha;
                g += pixel.Green * pixel.Alpha;
                b += pixel.Blue * pixel.Alpha;
                weight += pixel.Alpha;
            }

            if (weight == 0) return new Color();

            return new Color((byte) (r / weight), (byte) (g / weight), (byte) (b / weight));
        }
        catch (Exception)
        {
            return new Color();
        }
    }
}
