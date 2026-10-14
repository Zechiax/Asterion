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
        var snapshot = new Snapshot(project, version);

        foreach (var entry in candidateEntries)
        {
            if (!NotificationFilterEvaluator.ShouldNotify(entry, version))
                continue;

            db.PendingNotifications.Add(snapshot.CreateRow(entry.GuildId, NotificationKind.Update, entryId: entry.EntryId));
        }
    }

    public void EnqueueForFollows(DataContext db, Project project, Version version,
        IEnumerable<ModrinthFollow> candidateFollows, IReadOnlySet<ulong> guildIdsWithEntry)
    {
        var snapshot = new Snapshot(project, version);
        var notifiedGuilds = new HashSet<ulong>(guildIdsWithEntry);

        // Ordered so the same follow wins every time when a guild has several matching follows
        foreach (var follow in candidateFollows.OrderBy(f => f.Id))
        {
            // A manual entry for the project, or another follow of the same guild, already covers this version
            if (notifiedGuilds.Contains(follow.GuildId))
                continue;

            if (!follow.Events.HasFlag(FollowEvents.ProjectUpdate))
                continue;

            if (!NotificationFilterEvaluator.ShouldNotify(follow, version))
                continue;

            notifiedGuilds.Add(follow.GuildId);
            db.PendingNotifications.Add(snapshot.CreateRow(follow.GuildId, NotificationKind.Update, followId: follow.Id));
        }
    }

    public void EnqueueNewProject(DataContext db, Project project, Version latestVersion, ModrinthFollow follow)
    {
        var snapshot = new Snapshot(project, latestVersion);
        db.PendingNotifications.Add(snapshot.CreateRow(follow.GuildId, NotificationKind.NewProject, followId: follow.Id));
    }

    /// <summary>
    ///     Serializes the project and version lazily - only pay the cost if at least one row actually needs it.
    /// </summary>
    private sealed class Snapshot(Project project, Version version)
    {
        private readonly DateTime _now = DateTime.UtcNow;
        private string? _serializedProject;
        private string? _serializedVersion;

        public PendingNotification CreateRow(ulong guildId, NotificationKind kind, ulong? entryId = null,
            int? followId = null)
        {
            _serializedProject ??= JsonSerializer.Serialize(project);
            _serializedVersion ??= JsonSerializer.Serialize(version);

            return new PendingNotification
            {
                ProjectId = project.Id,
                VersionId = version.Id,
                SerializedProject = _serializedProject,
                SerializedVersion = _serializedVersion,
                GuildId = guildId,
                EntryId = entryId,
                FollowId = followId,
                Kind = kind,
                Status = NotificationStatus.Pending,
                CreatedAt = _now
            };
        }
    }
}
