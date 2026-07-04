using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Asterion.Database.Models;

[Table("PendingNotifications")]
public class PendingNotification
{
    [Key] public int Id { get; set; }

    [Required] public string ProjectId { get; set; } = null!;
    public virtual ModrinthProject Project { get; set; } = null!;

    [Required] public string VersionId { get; set; } = null!;

    // Snapshot of the Modrinth Project + Version at detection time, so dispatch
    // never needs to re-hit the Modrinth API and is immune to the project/version
    // having changed (or the API being down) by the time it's actually sent.
    [Required] public string SerializedProject { get; set; } = null!;
    [Required] public string SerializedVersion { get; set; } = null!;

    [Required] public ulong GuildId { get; set; }

    [Required] public ulong EntryId { get; set; }
    public virtual ModrinthEntry Entry { get; set; } = null!;

    [Required] public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    [Required] public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastAttemptAt { get; set; }

    // Null or in the past means "ready to send now"; a future value means "back off until then".
    public DateTime? NextRetryAt { get; set; }

    [Required] public DateTime CreatedAt { get; set; }
}

public enum NotificationStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    DeadLettered = 3
}
