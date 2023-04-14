using Discord.Interactions;
using Humanizer;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.SKCharts;
using LiveChartsCore.SkiaSharpView.VisualElements;
using Modrinth;
using SkiaSharp;

namespace Asterion.Modules;

#if DEBUG
public class DebugCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IModrinthClient _client;
    
    public DebugCommands(IModrinthClient client)
    {
        _client = client;
    }
    
    [SlashCommand("chart", "generate chart")]
    public async Task Chart(string projectId = "sodium")
    {
        await DeferAsync();
        
        var versions = (await _client.Version.GetProjectVersionListAsync(projectId)).ToList();
        versions = versions.OrderBy(x => x.DatePublished).ToList();
        
        var indexOfHighestDownloads = versions.IndexOf(versions.OrderByDescending(x => x.Downloads).First());
        Console.WriteLine(indexOfHighestDownloads);

        var cartesianChart = new SKCartesianChart()
        {
            Width = 1200,
            Height = 500,
            Series = new ISeries[]
            {
                new LineSeries<int>()
                {
                    Values = versions.Select(x => x.Downloads).ToList(),
                    Name = "Version Downloads",
                    // DataLabelsPosition = DataLabelsPosition.Top,
                    // DataLabelsSize = 20,
                    // DataLabelsPaint = new SolidColorPaint(SKColors.Blue),
                    // DataLabelsFormatter = (point) => point.PrimaryValue.ToMetric(decimals: 1).Transform(To.UpperCase),
                    // DataLabelsRotation = -45
                    GeometrySize = 10,
                }
            },
            XAxes = new List<Axis>
            {
                new Axis
                {
                    Labels = versions.Select(x => x.VersionNumber).ToList(),
                    LabelsRotation = 45,
                }
            },
            Title = new LabelVisual()
            {
                Text = "Downloads"
            },
            Background = SKColors.Azure
        };

        var image = cartesianChart.GetImage();

        Discord.FileAttachment file = new(image.Encode(SKEncodedImageFormat.Png, 100).AsStream(), "chart.png");

        await FollowupWithFileAsync(file);
    }
}
#endif