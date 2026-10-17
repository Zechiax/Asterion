using Microsoft.EntityFrameworkCore;

namespace Asterion.Database;

/// <summary>
///     Keeps only the latest TotalDownloads row per (project, day) for rows older than the cutoff.
/// </summary>
public static class StatisticsCleanup
{
    public const int BatchSize = 10_000;

    /// <returns>Number of removed rows</returns>
    public static async Task<int> TrimTotalDownloadsAsync(DataContext db, DateTime cutoff,
        CancellationToken ct = default)
    {
        var removed = 0;
        int batch;

        do
        {
            // n.Timestamp >= p.Timestamp lets SQLite seek the (ProjectId, Timestamp) index, without it the OR
            // makes it scan all rows of the project for every row
            batch = await db.TotalDownloads
                .Where(p => p.Timestamp < cutoff
                            && db.TotalDownloads.Any(n => n.ProjectId == p.ProjectId
                                                          && n.Timestamp >= p.Timestamp
                                                          && n.Timestamp < cutoff
                                                          && n.Timestamp < p.Timestamp.Date.AddDays(1)
                                                          && (n.Timestamp > p.Timestamp || n.Id > p.Id)))
                .OrderBy(p => p.Id)
                .Take(BatchSize)
                .ExecuteDeleteAsync(ct);

            removed += batch;
        } while (batch == BatchSize);

        return removed;
    }
}
