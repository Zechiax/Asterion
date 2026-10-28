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

    public int EnqueueMilestone(DataContext db, Project project, long previousDownloads, long currentDownloads,
        IEnumerable<ModrinthEntry> candidateEntries, IEnumerable<ModrinthFollow> candidateFollows)
    {
        var snapshot = new Snapshot(project, null);
        var entries = candidateEntries.OrderBy(e => e.EntryId).ToList();

        // A manual entry decides for its guild, even when it has milestones turned off
        var handledGuilds = entries.Select(e => e.GuildId).ToHashSet();
        var notifiedGuilds = new HashSet<ulong>();

        void TryAdd(ulong guildId, GuildSettings settings, ulong? entryId = null, int? followId = null)
        {
            if (!settings.MilestonesEnabled || notifiedGuilds.Contains(guildId))
                return;

            var milestone = DownloadMilestones.HighestCrossed(previousDownloads, currentDownloads,
                settings.MilestoneLadder);
            if (milestone is null)
                return;

            notifiedGuilds.Add(guildId);
            db.PendingNotifications.Add(snapshot.CreateRow(guildId, NotificationKind.DownloadMilestone, entryId,
                followId, milestone));
        }

        foreach (var entry in entries.Where(e => e.NotifyMilestones))
            TryAdd(entry.GuildId, entry.Guild.GuildSettings, entryId: entry.EntryId);

        // Ordered so the same follow wins every time when a guild has several matching follows
        foreach (var follow in candidateFollows.OrderBy(f => f.Id))
        {
            if (handledGuilds.Contains(follow.GuildId) || !follow.Events.HasFlag(FollowEvents.DownloadMilestone))
                continue;

            TryAdd(follow.GuildId, follow.Guild.GuildSettings, followId: follow.Id);
        }

        // One row per notified guild
        return notifiedGuilds.Count;
    }

    /// <summary>
    ///     Serializes the project and version lazily - only pay the cost if at least one row actually needs it.
    /// </summary>
    /// <param name="version">Null for notifications that are not about a version, e.g. milestones</param>
    private sealed class Snapshot(Project project, Version? version)
    {
        private readonly DateTime _now = DateTime.UtcNow;
        private string? _serializedProject;
        private string? _serializedVersion;

        public PendingNotification CreateRow(ulong guildId, NotificationKind kind, ulong? entryId = null,
            int? followId = null, long? milestone = null)
        {
            _serializedProject ??= JsonSerializer.Serialize(project);
            if (version is not null)
                _serializedVersion ??= JsonSerializer.Serialize(version);

            return new PendingNotification
            {
                ProjectId = project.Id,
                VersionId = version?.Id,
                SerializedProject = _serializedProject,
                SerializedVersion = _serializedVersion,
                Milestone = milestone,
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
