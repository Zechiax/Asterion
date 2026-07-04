using System.Text.Json;
using Asterion.Database;
using Asterion.Database.Models;
using Modrinth.Models;
using Version = Modrinth.Models.Version;

namespace Asterion.Services.Notifications;

public class NotificationScheduler : INotificationScheduler
{
    public void EnqueueForEntries(DataContext db, Project project, Version version, IEnumerable<ModrinthEntry> candidateEntries)
    {
        string? serializedProject = null;
        string? serializedVersion = null;
        var now = DateTime.UtcNow;

        foreach (var entry in candidateEntries)
        {
            if (!NotificationFilterEvaluator.ShouldNotify(entry, version))
                continue;

            // Serialize lazily - only pay the cost if at least one entry actually needs a row
            serializedProject ??= JsonSerializer.Serialize(project);
            serializedVersion ??= JsonSerializer.Serialize(version);

            db.PendingNotifications.Add(new PendingNotification
            {
                ProjectId = project.Id,
                VersionId = version.Id,
                SerializedProject = serializedProject,
                SerializedVersion = serializedVersion,
                GuildId = entry.GuildId,
                EntryId = entry.EntryId,
                Status = NotificationStatus.Pending,
                CreatedAt = now
            });
        }
    }
}
