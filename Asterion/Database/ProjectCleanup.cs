using Microsoft.EntityFrameworkCore;

namespace Asterion.Database;

/// <summary>
///     Removes ModrinthProject rows that nothing uses anymore. A project is used by a manual entry
///     (ModrinthEntry) or by a followed project (FollowProject). Both must be counted, otherwise shared
///     project state that a follow still needs would be deleted (and would cascade to outbox rows).
/// </summary>
public static class ProjectCleanup
{
    /// <summary>
    ///     Removes the given projects if they are unused. Pass null to check every project.
    /// </summary>
    /// <returns>IDs of the removed projects</returns>
    public static async Task<IList<string>> RemoveUnusedProjectsAsync(DataContext db,
        IEnumerable<string>? projectIds = null, CancellationToken ct = default)
    {
        var query = db.ModrinthProjects.AsQueryable();

        if (projectIds is not null)
        {
            var ids = projectIds.Distinct().ToList();
            if (ids.Count == 0)
                return [];

            query = query.Where(p => ids.Contains(p.ProjectId));
        }

        var unused = await query
            .Where(p => !db.ModrinthEntries.Any(e => e.ProjectId == p.ProjectId)
                        && !db.FollowProjects.Any(fp => fp.ProjectId == p.ProjectId))
            .ToListAsync(ct);

        if (unused.Count == 0)
            return [];

        db.ModrinthProjects.RemoveRange(unused);
        await db.SaveChangesAsync(ct);

        return unused.Select(p => p.ProjectId).ToList();
    }
}
