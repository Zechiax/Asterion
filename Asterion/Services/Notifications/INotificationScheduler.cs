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
}
