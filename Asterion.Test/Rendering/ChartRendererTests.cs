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
}
