using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Asterion.Database.Models;

[Table("PendingNotifications")]
public class PendingNotification
{
    [Key] public int Id { get; set; }

    [Required] public string ProjectId { get; set; } = null!;
    public virtual ModrinthProject Project { get; set; } = null!;

    // Null for milestones, they are not about a version
    public string? VersionId { get; set; }

    // Snapshot of the Modrinth Project + Version at detection time, so dispatch
    // never needs to re-hit the Modrinth API and is immune to the project/version
    // having changed (or the API being down) by the time it's actually sent.
    [Required] public string SerializedProject { get; set; } = null!;
    public string? SerializedVersion { get; set; }

    /// <summary>The download milestone reached, set only for <see cref="NotificationKind.DownloadMilestone" /></summary>
    public long? Milestone { get; set; }

    [Required] public ulong GuildId { get; set; }

    // Exactly one of EntryId (manual subscription) or FollowId (followed target) is set.
    public ulong? EntryId { get; set; }
    public virtual ModrinthEntry? Entry { get; set; }

    public int? FollowId { get; set; }
    public virtual ModrinthFollow? Follow { get; set; }

    [Required] public NotificationKind Kind { get; set; } = NotificationKind.Update;

    [Required] public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    [Required] public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastAttemptAt { get; set; }

    // Null or in the past means "ready to send now"; a future value means "back off until then".
    public DateTime? NextRetryAt { get; set; }

    [Required] public DateTime CreatedAt { get; set; }
}

public enum NotificationKind
{
    /// <summary>A project got a new version.</summary>
    Update = 0,

    /// <summary>A followed target published a new project.</summary>
    NewProject = 1,

    /// <summary>A project reached a download milestone.</summary>
    DownloadMilestone = 2
}

public enum NotificationStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    DeadLettered = 3
}
