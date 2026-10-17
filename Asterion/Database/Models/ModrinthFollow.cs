using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Asterion.Database.Models;

/// <summary>
///     A guild following a target (a Modrinth user, later also an organization).
///     Updates are sent for every project of the target, including projects published later.
/// </summary>
[Table("ModrinthFollows")]
public class ModrinthFollow : IReleaseFilterable
{
    [Key] public int Id { get; set; }

    [Required] public ulong GuildId { get; set; }
    public virtual Guild Guild { get; set; } = null!;

    [Required] public FollowTargetType TargetType { get; set; }

    /// <summary>Modrinth ID of the target (never the username, it can change).</summary>
    [Required] public string TargetId { get; set; } = null!;

    /// <summary>Display name of the target, refreshed on each sync.</summary>
    [Required] public string TargetName { get; set; } = null!;

    [Required] public FollowEvents Events { get; set; } = FollowEvents.ProjectUpdate | FollowEvents.NewProject;

    public ulong? CustomUpdateChannel { get; set; }

    /// <summary>Channel for new project messages, falls back to <see cref="CustomUpdateChannel" />.</summary>
    public ulong? NewProjectChannel { get; set; }

    /// <summary>Ping role for update messages.</summary>
    public ulong? CustomPingRole { get; set; }

    /// <summary>Ping role for new project messages, falls back to <see cref="CustomPingRole" />.</summary>
    public ulong? NewProjectPingRole { get; set; }

    [Required] public ReleaseType ReleaseFilter { get; set; } = ReleaseType.Alpha | ReleaseType.Beta | ReleaseType.Release;

    [NotMapped]
    public string[]? LoaderFilter
    {
        get => SerializedLoaderFilter == null
            ? null
            : JsonSerializer.Deserialize<string[]>(SerializedLoaderFilter);
        set => SerializedLoaderFilter = value == null
            ? null
            : JsonSerializer.Serialize(value);
    }

    public string? SerializedLoaderFilter { get; private set; }

    [Required] public DateTime Created { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public string? LastSyncError { get; set; }

    public virtual ICollection<FollowProject> Projects { get; set; } = new List<FollowProject>();
}

public enum FollowTargetType
{
    User = 0,
    Organization = 1
}

[Flags]
public enum FollowEvents
{
    None = 0,
    ProjectUpdate = 1,
    NewProject = 2
}
