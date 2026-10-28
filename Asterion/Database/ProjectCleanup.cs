using Asterion.Database.Models;
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
    ///     Removes the given projects if they are unused
    /// </summary>
    /// <returns>IDs of the removed projects</returns>
    public static async Task<IList<string>> RemoveUnusedProjectsAsync(DataContext db,
        IEnumerable<string> projectIds, CancellationToken ct = default)
    {
        var ids = projectIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var unused = await WhereUnused(db.ModrinthProjects.Where(p => ids.Contains(p.ProjectId)), db)
            .ToListAsync(ct);

        if (unused.Count == 0)
            return [];

        db.ModrinthProjects.RemoveRange(unused);
        await db.SaveChangesAsync(ct);

        return unused.Select(p => p.ProjectId).ToList();
    }

    /// <summary>
    ///     Removes every unused project in a single statement, so an entry or follow added meanwhile
    ///     can't lose its project (the delete would cascade to it)
    /// </summary>
    /// <returns>Number of removed projects</returns>
    public static Task<int> RemoveAllUnusedProjectsAsync(DataContext db, CancellationToken ct = default)
    {
        return WhereUnused(db.ModrinthProjects, db).ExecuteDeleteAsync(ct);
    }

    private static IQueryable<ModrinthProject> WhereUnused(IQueryable<ModrinthProject> projects, DataContext db)
    {
        return projects.Where(p => !db.ModrinthEntries.Any(e => e.ProjectId == p.ProjectId)
                                   && !db.FollowProjects.Any(fp => fp.ProjectId == p.ProjectId));
    }
}
