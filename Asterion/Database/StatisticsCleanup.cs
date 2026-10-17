using Microsoft.EntityFrameworkCore;

namespace Asterion.Database;

/// <summary>
///     Keeps only the latest TotalDownloads row per (project, day) for rows older than the cutoff.
/// </summary>
public static class StatisticsCleanup
{
    public const int BatchSize = 10_000;

    // Lets other writers in between batches, so they don't time out waiting for the lock
    private static readonly TimeSpan BatchPause = TimeSpan.FromMilliseconds(250);

    /// <returns>Number of removed rows</returns>
    public static async Task<int> TrimTotalDownloadsAsync(DataContext db, DateTime cutoff,
        CancellationToken ct = default)
    {
        var old = db.TotalDownloads.Where(p => p.Timestamp < cutoff);
        var firstId = await old.MinAsync(p => (int?)p.Id, ct);
        var lastId = await old.MaxAsync(p => (int?)p.Id, ct);
        if (firstId is null || lastId is null)
            return 0;

        var removed = 0;

        // Batches are Id ranges, so each one touches at most BatchSize rows
        for (var from = firstId.Value; from <= lastId; from += BatchSize)
        {
            var to = from + BatchSize;

            removed += await old
                .Where(p => p.Id >= from && p.Id < to
                            && db.TotalDownloads.Any(n => n.ProjectId == p.ProjectId
                                                          && n.Timestamp >= p.Timestamp
                                                          && n.Timestamp < p.Timestamp.Date.AddDays(1)
                                                          && (n.Timestamp > p.Timestamp || n.Id > p.Id)))
                .ExecuteDeleteAsync(ct);

            await Task.Delay(BatchPause, ct);
        }

        return removed;
    }
}
