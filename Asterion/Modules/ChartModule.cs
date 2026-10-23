using Asterion.AutocompleteHandlers;
using Asterion.Common;
using Asterion.Interfaces;
using Asterion.Services;
using Asterion.Services.Modrinth;
using Asterion.Services.Statistics;
using Discord;
using Discord.Interactions;

namespace Asterion.Modules;

public enum ChartRange
{
    [ChoiceDisplay("24 hours")] Day,
    [ChoiceDisplay("7 days")] Week,
    [ChoiceDisplay("30 days")] Month,
    [ChoiceDisplay("90 days")] Quarter,
    [ChoiceDisplay("1 year")] Year
}

public enum ChartMetric
{
    Downloads,
    Followers
}

[Group("chart", "Creates charts of project statistics")]
// ReSharper disable once ClassNeverInstantiated.Global
public class ChartModule(
    ILocalizationService localizationService,
    IGuildCultureProvider cultureProvider,
    ModrinthService modrinthService,
    ProjectStatisticsManager statistics)
    : AsterionInteractionModuleBase(localizationService, cultureProvider)
{
    // Fewer points than this don't make a useful chart
    private const int MinPoints = 5;

    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
    private static readonly TimeSpan Day = TimeSpan.FromDays(1);

    [SlashCommand("downloads", "Charts the downloads of a project", runMode: RunMode.Async)]
    public Task Downloads(
        [Summary("project", "ID or slug of the project")] [Autocomplete(typeof(SubscribedIdAutocompletionHandler))]
        string project,
        [Summary("range", "Time span of the chart")]
        ChartRange range = ChartRange.Month)
    {
        return SendChartAsync(project, ChartMetric.Downloads, range);
    }

    [SlashCommand("followers", "Charts the new followers of a project", runMode: RunMode.Async)]
    public Task Followers(
        [Summary("project", "ID or slug of the project")] [Autocomplete(typeof(SubscribedIdAutocompletionHandler))]
        string project,
        [Summary("range", "Time span of the chart")]
        ChartRange range = ChartRange.Month)
    {
        return SendChartAsync(project, ChartMetric.Followers, range);
    }

    private async Task SendChartAsync(string query, ChartMetric metric, ChartRange range)
    {
        await DeferAsync();

        var search = await modrinthService.FindProject(query);
        if (!search.Success)
        {
            await SendStatusAsync(Errors.FromSearch(search));
            return;
        }

        var project = search.Payload.Project;
        var (hourly, step, count, labelFormat) = Buckets(range);
        var unit = hourly ? Hour : Day;
        var now = DateTime.UtcNow;

        // One extra unit before the first bucket, its sample is the base of the first change
        var from = now - count * step - step - unit;
        var samples = hourly
            ? (await statistics.GetHourlyAsync(project.Id, from))
            .ToDictionary(s => s.Hour, s => metric == ChartMetric.Downloads ? s.Downloads : s.Followers)
            : (await statistics.GetDailyAsync(project.Id, from))
            .ToDictionary(s => s.Day, s => metric == ChartMetric.Downloads ? s.Downloads : s.Followers);

        if (samples.Count == 0)
        {
            await SendStatusAsync(Errors.NoStatsData);
            return;
        }

        var points = StatisticsSeries.BuildDeltas(samples, now, unit, step, count);
        if (points.Count(p => p.Value is not null) < MinPoints)
        {
            await SendStatusAsync(Errors.NotEnoughData(step * (MinPoints + 1)));
            return;
        }

        var axisName = hourly
            ? LocalizationService.Get("chart-axis-hour", CommandCultureInfo)
            : LocalizationService.Get("chart-axis-day", CommandCultureInfo);
        var image = ChartRenderer.RenderPng(points, labelFormat, axisName, project.Color,
            CommandCultureInfo);

        var args = new { title = Format.Sanitize(project.Title), range = range.ToString().ToLowerInvariant() };
        var caption = metric == ChartMetric.Downloads
            ? LocalizationService.Get("chart-caption-downloads", CommandCultureInfo, args)
            : LocalizationService.Get("chart-caption-followers", CommandCultureInfo, args);

        await FollowupWithFileAsync(new FileAttachment(image, "chart.png"), caption);
    }

    /// <returns>If the chart uses the hourly samples, the length and number of buckets and the label format</returns>
    private static (bool Hourly, TimeSpan Step, int Count, string LabelFormat) Buckets(ChartRange range)
    {
        return range switch
        {
            ChartRange.Day => (true, Hour, 24, "HH:mm"),
            ChartRange.Week => (true, TimeSpan.FromHours(6), 28, "dd/MM HH:mm"),
            ChartRange.Month => (false, Day, 30, "dd/MM"),
            ChartRange.Quarter => (false, Day, 90, "dd/MM"),
            ChartRange.Year => (false, Day, 365, "dd/MM/yy"),
            _ => throw new ArgumentOutOfRangeException(nameof(range), range, null)
        };
    }
}
