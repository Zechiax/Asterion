using System.Globalization;
using Asterion.Services.Statistics;
using LiveChartsCore;
using LiveChartsCore.Defaults;
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
    /// <param name="timeAxis">Spacing and format of the X axis labels</param>
    /// <param name="color">Line color, e.g. the project's color</param>
    /// <param name="culture">Culture of the labels, invariant if null</param>
    public static Stream RenderPng(IReadOnlyList<ChartPoint> points, TimeAxis timeAxis, System.Drawing.Color? color,
        CultureInfo? culture)
    {
        culture ??= CultureInfo.InvariantCulture;
        var line = color is { } c ? new SKColor(c.R, c.G, c.B) : DefaultLine;
        var markers = points.Count <= MaxPointsWithMarkers;

        // X is the time in label units since a Monday, so the labels land on whole hours, days or weeks,
        // the same Mondays the weekly buckets start on
        var values = points
            .Select(p => new ObservablePoint((p.Start - DateTime.MinValue) / timeAxis.Unit, p.Value))
            .ToArray();

        var chart = new SKCartesianChart
        {
            Width = Width,
            Height = Height,
            Background = Background,
            LegendPosition = LegendPosition.Hidden,
            Series =
            [
                new LineSeries<ObservablePoint>
                {
                    Values = values,
                    Stroke = new SolidColorPaint(line) { StrokeThickness = 3 },
                    Fill = new SolidColorPaint(line.WithAlpha(40)),
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
                    Labeler = x => TimeLabel(x, timeAxis, culture),
                    MinStep = timeAxis.Step,
                    ForceStepToMin = true,
                    LabelsPaint = new SolidColorPaint(Text),
                    SeparatorsPaint = null
                }
            },
            YAxes = new[]
            {
                new Axis
                {
                    // From zero, unless there are negative values, e.g. lost followers
                    MinLimit = points.Any(p => p.Value < 0) ? null : 0,
                    Labeler = value => Compact(value, culture),
                    LabelsPaint = new SolidColorPaint(Text),
                    SeparatorsPaint = new SolidColorPaint(Separators) { StrokeThickness = 1 }
                }
            }
        };

        using var image = chart.GetImage();
        return image.Encode(SKEncodedImageFormat.Png, 100).AsStream();
    }

    private static string TimeLabel(double x, TimeAxis timeAxis, IFormatProvider culture)
    {
        // LiveCharts also asks for labels of extreme values when measuring them
        var ticks = x * timeAxis.Unit.Ticks;
        if (double.IsNaN(ticks) || ticks < 0 || ticks > DateTime.MaxValue.Ticks)
            return string.Empty;

        return new DateTime((long)ticks).ToString(timeAxis.LabelFormat, culture);
    }

    /// <summary>
    ///     Short form of large numbers, e.g. 1.2M or 35k
    /// </summary>
    private static string Compact(double value, IFormatProvider culture)
    {
        return Math.Abs(value) switch
        {
            >= 1_000_000 => (value / 1_000_000).ToString("0.#", culture) + "M",
            >= 1_000 => (value / 1_000).ToString("0.#", culture) + "k",
            _ => value.ToString("0.#", culture)
        };
    }
}

/// <param name="Unit">Time of one step on the axis</param>
/// <param name="Step">Steps between the labels</param>
/// <param name="LabelFormat">Format of the label times</param>
public sealed record TimeAxis(TimeSpan Unit, double Step, string LabelFormat);
