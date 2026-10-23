using Asterion.Services.Statistics;

namespace Asterion.Test.Statistics;

[TestFixture]
public class StatisticsSeriesTests
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
    private static readonly DateTime Now = new(2026, 10, 23, 12, 30, 0, DateTimeKind.Utc);

    // Hourly samples from 05:00 to 12:00 growing by 10 each hour
    private static Dictionary<DateTime, int> Samples(int? missingHour = null)
    {
        var samples = new Dictionary<DateTime, int>();
        for (var hour = 5; hour <= 12; hour++)
            if (hour != missingHour)
                samples[new DateTime(2026, 10, 23, hour, 0, 0, DateTimeKind.Utc)] = hour * 10;

        return samples;
    }

    [Test]
    public void DeltasSkipTheCurrentBucketAndLeaveGapsForMissingSamples()
    {
        var points = StatisticsSeries.BuildDeltas(Samples(missingHour: 9), Now, Hour, Hour, 5);

        // 07:00 to 11:00, the 12:00 hour is still in progress
        Assert.That(points.Select(p => p.Start.Hour), Is.EqualTo(new[] { 7, 8, 9, 10, 11 }));
        Assert.That(points.Select(p => p.Value), Is.EqualTo(new int?[] { 10, 10, null, null, 10 }));
    }

    [Test]
    public void DeltasOfLongerBucketsSpanTheirUnits()
    {
        var points = StatisticsSeries.BuildDeltas(Samples(), Now, Hour, TimeSpan.FromHours(2), 3);

        // Buckets aligned to 2 hours, each the change from the sample before it to its last hour's sample
        Assert.That(points.Select(p => p.Start.Hour), Is.EqualTo(new[] { 6, 8, 10 }));
        Assert.That(points.Select(p => p.Value), Is.EqualTo(new int?[] { 20, 20, 20 }));
    }
}
