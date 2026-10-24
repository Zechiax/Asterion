using Asterion.Database;
using Asterion.Database.Models;
using Modrinth.Models;
using Version = Modrinth.Models.Version;

namespace Asterion.Services.Notifications;

public interface INotificationScheduler
{
    /// <summary>
    ///     Adds PendingNotification rows to the given DataContext for every entry that
    ///     should be notified about this version, applying release/loader filters.
    /// </summary>
    void EnqueueForEntries(DataContext db, Project project, Version version, IEnumerable<ModrinthEntry> candidateEntries);

    /// <summary>
    ///     Adds update PendingNotification rows for follows that track this project, applying release/loader filters.
    ///     Sends at most one row per guild: guilds in <paramref name="guildIdsWithEntry" /> are skipped (the manual
    ///     entry wins), and only the first matching follow of a guild gets a row.
    /// </summary>
    /// <param name="candidateFollows">Follows with a non-ignored FollowProject row for this project</param>
    /// <param name="guildIdsWithEntry">Guilds that have a manual entry for this project</param>
    void EnqueueForFollows(DataContext db, Project project, Version version, IEnumerable<ModrinthFollow> candidateFollows,
        IReadOnlySet<ulong> guildIdsWithEntry);

    /// <summary>
    ///     Adds a NewProject PendingNotification row for the follow. Filters do not apply.
    /// </summary>
    void EnqueueNewProject(DataContext db, Project project, Version latestVersion, ModrinthFollow follow);

    /// <summary>
    ///     Adds DownloadMilestone PendingNotification rows for guilds with milestones enabled whose ladder has a
    ///     milestone between the two download counts. Sends at most one row per guild: an entry decides for its
    ///     guild (no row if it has milestones turned off), otherwise the first follow with the milestone event.
    /// </summary>
    /// <param name="candidateEntries">Entries of the project, with Guild.GuildSettings loaded</param>
    /// <param name="candidateFollows">Follows tracking the project, with Guild.GuildSettings loaded</param>
    void EnqueueMilestone(DataContext db, Project project, long previousDownloads, long currentDownloads,
        IEnumerable<ModrinthEntry> candidateEntries, IEnumerable<ModrinthFollow> candidateFollows);
}
