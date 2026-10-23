namespace Asterion.Services.Statistics;

/// <param name="Start">UTC start of the bucket</param>
/// <param name="Value">Change during the bucket, null if a sample around it is missing</param>
public readonly record struct ChartPoint(DateTime Start, int? Value);

public static class StatisticsSeries
{
    /// <summary>
    ///     Turns cumulative samples into the change per bucket, for the <paramref name="count" /> last complete buckets
    /// </summary>
    /// <param name="samples">Cumulative values keyed by the UTC start of their sample unit (hour or day),
    ///     each holding the last value seen in that unit</param>
    /// <param name="now">Current time, the bucket containing it is not complete and is left out</param>
    /// <param name="unit">Length of a sample unit</param>
    /// <param name="step">Length of a bucket, a multiple of <paramref name="unit" /></param>
    /// <param name="count">Number of buckets</param>
    public static IReadOnlyList<ChartPoint> BuildDeltas(IReadOnlyDictionary<DateTime, int> samples, DateTime now,
        TimeSpan unit, TimeSpan step, int count)
    {
        var lastStart = Floor(now, step) - step;
        var points = new ChartPoint[count];

        for (var i = 0; i < count; i++)
        {
            var start = lastStart - (count - 1 - i) * step;

            // The sample of the bucket's last unit minus the sample of the unit just before the bucket
            int? value = samples.TryGetValue(start + step - unit, out var end)
                         && samples.TryGetValue(start - unit, out var before)
                ? end - before
                : null;

            points[i] = new ChartPoint(start, value);
        }

        return points;
    }

    private static DateTime Floor(DateTime time, TimeSpan step)
    {
        return new DateTime(time.Ticks - time.Ticks % step.Ticks, DateTimeKind.Utc);
    }
}
