using System.Drawing;
using Asterion.Common;
using Asterion.Services.Statistics;
using SkiaSharp;

namespace Asterion.Test.Rendering;

/// <summary>
/// Tests so that we are sure LiveCharts are rendering on Linux correctly
/// </summary>
[TestFixture]
public class ChartRendererTests
{
    [Test]
    public void RendersTheLineIntoAPng()
    {
        var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        int?[] values = [120, 340, null, 250, 410, 380];
        var points = values.Select((value, i) => new ChartPoint(start.AddDays(i), value)).ToList();

        using var png = ChartRenderer.RenderPng(points, new TimeAxis(TimeSpan.FromDays(1), 1, "dd/MM"),
            Color.FromArgb(255, 0, 0), null);
        using var bitmap = SKBitmap.Decode(png);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bitmap, Is.Not.Null);
            Assert.That((bitmap.Width, bitmap.Height), Is.EqualTo((1000, 500)));
        }
        Assert.That(bitmap.Pixels, Has.Some.EqualTo(new SKColor(255, 0, 0)), "The line was not drawn");
    }

    // A dark project color is lightened to stand out, keeping its hue, a light one is used as it is
    [TestCase(13, 55, 112, true)]
    [TestCase(255, 170, 0, false)]
    public void ProjectColorStandsOutFromTheBackground(int r, int g, int b, bool lightened)
    {
        var line = ChartRenderer.LineColor(Color.FromArgb(r, g, b));

        Assert.That(ChartRenderer.ContrastRatio(line, ChartRenderer.Background),
            Is.GreaterThanOrEqualTo(ChartRenderer.MinLineContrast));
        Assert.That(line != new SKColor((byte)r, (byte)g, (byte)b), Is.EqualTo(lightened));

        new SKColor((byte)r, (byte)g, (byte)b).ToHsl(out var hue, out _, out _);
        line.ToHsl(out var lineHue, out _, out _);
        Assert.That(lineHue, Is.EqualTo(hue).Within(2));
    }
}
