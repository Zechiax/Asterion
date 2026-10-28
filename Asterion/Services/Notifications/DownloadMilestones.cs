using Asterion.Database.Models;

namespace Asterion.Services.Notifications;

public static class DownloadMilestones
{
    /// <summary>
    ///     The highest milestone of the ladder passed when the downloads went from <paramref name="previous" /> to
    ///     <paramref name="current" />, null if none was. Milestones skipped over in one jump are not returned.
    /// </summary>
    public static long? HighestCrossed(long previous, long current, MilestoneLadder ladder)
    {
        long? highest = null;

        foreach (var milestone in Milestones(ladder))
        {
            if (milestone > current)
                break;

            if (milestone > previous)
                highest = milestone;
        }

        return highest;
    }

    /// <summary>
    ///     Milestones of the ladder in ascending order
    /// </summary>
    public static IEnumerable<long> Milestones(MilestoneLadder ladder)
    {
        var start = ladder == MilestoneLadder.Major ? 100_000L : 1_000L;

        // Stops well before overflowing, download counts are far below that
        for (var power = start; power <= long.MaxValue / 100; power *= 10)
        {
            yield return power;

            if (ladder != MilestoneLadder.Standard)
                continue;

            // 2.5x and 5x of the power of ten
            yield return power * 5 / 2;
            yield return power * 5;
        }
    }
}
