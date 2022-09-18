using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace RinthBot.Services;

public static class ImageService
{
    public static async Task<Rgb24> GetMajorColorFromImageUrl(this HttpClient client, string imageUrl)
    {
        try
        {
            var stream = await client.GetStreamAsync(imageUrl);

            using var image = await Image.LoadAsync<Rgb24>(stream);
            image.Mutate(
                x => x
                    // Scale the image down preserving the aspect ratio. This will speed up quantization.
                    // We use nearest neighbor as it will be the fastest approach.
                    .Resize(new ResizeOptions {Sampler = KnownResamplers.NearestNeighbor, Size = new Size(100, 0)})

                    // Reduce the color palette to 1 color without dithering.
                    .Quantize(new OctreeQuantizer(new QuantizerOptions
                    {
                        DitherScale = 0,
                        MaxColors = 1,
                        Dither = null
                    })));

            var dominant = image[0, 0];

            return dominant;
        }
        catch (Exception)
        {
            return new Rgb24();
        }
    }
}