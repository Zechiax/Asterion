using System.Net;
using Asterion.Services;
using SkiaSharp;

namespace Asterion.Test.Rendering;

[TestFixture]
public class ImageServiceTests
{
    // GetMajorColorFromImageUrl swallows exceptions and returns no color, so check the color itself
    [Test]
    public async Task MajorColorOfASingleColorImage()
    {
        using var bitmap = new SKBitmap(64, 32);
        bitmap.Erase(new SKColor(200, 40, 10));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        using var client = new HttpClient(new FixedResponseHandler(data.ToArray()));
        var color = await client.GetMajorColorFromImageUrl("https://cdn.modrinth.com/icon.png");

        Assert.That((color.R, color.G, color.B), Is.EqualTo((200, 40, 10)));
    }

    private class FixedResponseHandler(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
        }
    }
}
