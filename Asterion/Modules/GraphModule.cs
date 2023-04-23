using Asterion.Services;
using Asterion.Services.Modrinth;
using Discord.Commands;
using Discord.Interactions;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.SKCharts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using RunMode = Discord.Interactions.RunMode;

namespace Asterion.Modules;

public class GraphModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DownloadManager _downloadManager;
    private readonly ModrinthService _modrinthService;
    private readonly ILogger<GraphModule> _logger;
    
    public GraphModule(IServiceProvider serviceProvider)
    {
        _downloadManager = serviceProvider.GetRequiredService<DownloadManager>();
        _modrinthService = serviceProvider.GetRequiredService<ModrinthService>();
        _logger = serviceProvider.GetRequiredService<ILogger<GraphModule>>();
    }
    
    // We create subcommands for each graph type
    [Discord.Interactions.Group("chart", "Creates charts of different statistics")]
    public class ChartType : GraphModule
    {
        [SlashCommand("24h", "Graphs the downloads of a project over the span of 24 hours", runMode: RunMode.Async)]
        public async Task Hourly(string projectId = "fabric-api")
        {
            await DeferAsync();
    
            var project = await _modrinthService.FindProject(projectId);
            if (project.Success == false)
            {
                await FollowupAsync("Project not found", ephemeral: true);
                return;
            }

            var downloadData = (await _downloadManager.GetTotalDownloadsAsync(project.Payload.Project.Id)).OrderBy(x => x.Timestamp).ToList();
            
            if (downloadData.Count == 0)
            {
                await FollowupAsync("No downloads are currently stored", ephemeral: true);
                return;
            }
            
            _logger.LogDebug("Download data: {@DownloadData}", downloadData);
            
            // We only want the last 24 hours and only pick the last measurement of each hour
            var now = DateTime.UtcNow;
            downloadData = downloadData.Where(x => x.Timestamp > now.AddHours(-24)).ToList();
            var lastHour = -1;
            for (var i = 0; i < downloadData.Count; i++)
            {
                if (downloadData[i].Timestamp.Hour == lastHour)
                {
                    downloadData.RemoveAt(i);
                    i--;
                }
                else
                {
                    lastHour = downloadData[i].Timestamp.Hour;
                }
            }
            
            // We check if we have enough data to create a graph
            if (downloadData.Count < 5)
            {
                _logger.LogDebug("Not enough data to create a graph, we have {DownloadDataCount} data points", downloadData.Count);
                await FollowupAsync("Not enough data to create a graph, please wait for a bit", ephemeral: true);
                return;
            }

            // Make the downloads be a difference between each other, so the graph shows the number of downloads each time
            var downloads = new List<int>();
            for (var i = 0; i < downloadData.Count; i++)
            {
                if (i == 0)
                {
                    // downloads.Add(downloadData[i].Downloads);
                    // We'll skip the first one, so the result is a difference between the first and second
                    continue;
                }

                downloads.Add(downloadData[i].Downloads - downloadData[i - 1].Downloads);
            }

            var color = project.Payload.Project.Color!.Value;

            var cartesianChart = new SKCartesianChart()
            {
                Series = new ISeries[]
                {
                    new ColumnSeries<int>
                    {
                        Values = downloads,
                        Stroke = new SolidColorPaint(new SKColor(color.R, color.G, color.B)),
                        // Fill = new SolidColorPaint(new SKColor(color.R, color.G, color.B, 100))
                    }
                },
                XAxes = new[]
                {
                    new Axis
                    {
                        // We don't need to display minutes
                        Labels = downloadData.Select(x => x.Timestamp.ToString("HH")).ToArray()
                    }
                },
                // YAxes = new[]
                // {
                //     new Axis
                //     {
                //         Labels = downloads.Select(x => x.ToString()).ToArray()
                //     }
                // }
            };
    
            var image = cartesianChart.GetImage();
    
            Discord.FileAttachment file = new(image.Encode(SKEncodedImageFormat.Png, 100).AsStream(), "chart.png");
    
            await FollowupWithFileAsync(file, "Downloads of " + project.Payload.Project.Title);
        }

        public ChartType(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }
    }
}