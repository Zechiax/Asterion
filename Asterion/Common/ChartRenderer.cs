using System.Globalization;
using Asterion.Services.Statistics;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.SKCharts;
using SkiaSharp;

namespace Asterion.Common;

/// <summary>
///     Renders statistics as a PNG line chart styled for Discord's dark theme
/// </summary>
public static class ChartRenderer
{
    private const int Width = 1000;
    private const int Height = 500;

    // Above this many points the markers only clutter the line
    private const int MaxPointsWithMarkers = 60;

    private static readonly SKColor Background = SKColor.Parse("#313338");
    private static readonly SKColor Text = SKColor.Parse("#DBDEE1");
    private static readonly SKColor Separators = SKColor.Parse("#3F4147");
    private static readonly SKColor DefaultLine = SKColor.Parse("#1BD96A");

    /// <param name="points">Values of the chart, null values are drawn as gaps</param>
    /// <param name="labelFormat">Format of the bucket start in the X axis labels</param>
    /// <param name="axisName">Name of the X axis</param>
    /// <param name="color">Line color, e.g. the project's color</param>
    /// <param name="culture">Culture of the labels, invariant if null</param>
    public static Stream RenderPng(IReadOnlyList<ChartPoint> points, string labelFormat, string axisName,
        System.Drawing.Color? color, CultureInfo? culture)
    {
        culture ??= CultureInfo.InvariantCulture;
        var line = color is { } c ? new SKColor(c.R, c.G, c.B) : DefaultLine;
        var markers = points.Count <= MaxPointsWithMarkers;

        var chart = new SKCartesianChart
        {
            Width = Width,
            Height = Height,
            Background = Background,
            LegendPosition = LegendPosition.Hidden,
            Series =
            [
                new LineSeries<int?>
                {
                    Values = points.Select(p => p.Value).ToArray(),
                    Stroke = new SolidColorPaint(line) { StrokeThickness = 3 },
                    Fill = null,
                    GeometrySize = markers ? 8 : 0,
                    GeometryStroke = markers ? new SolidColorPaint(line) { StrokeThickness = 3 } : null,
                    GeometryFill = markers ? new SolidColorPaint(Background) : null,
                    LineSmoothness = 0
                }
            ],
            XAxes = new[]
            {
                new Axis
                {
                    Name = axisName,
                    Labels = points.Select(p => p.Start.ToString(labelFormat, culture)).ToArray(),
                    LabelsRotation = points.Count > 24 ? 45 : 0,
                    NamePaint = new SolidColorPaint(Text),
                    LabelsPaint = new SolidColorPaint(Text),
                    SeparatorsPaint = null
                }
            },
            YAxes = new[]
            {
                new Axis
                {
                    Labeler = value => value.ToString("N0", culture),
                    LabelsPaint = new SolidColorPaint(Text),
                    SeparatorsPaint = new SolidColorPaint(Separators) { StrokeThickness = 1 }
                }
            }
        };

        using var image = chart.GetImage();
        return image.Encode(SKEncodedImageFormat.Png, 100).AsStream();
    }
}
