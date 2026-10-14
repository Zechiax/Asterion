using Asterion.Database.Models;
using Modrinth.Models;

namespace Asterion.Services.Follows;

/// <summary>
///     Lists the current projects of a follow target. One implementation per <see cref="FollowTargetType" />.
/// </summary>
public interface IFollowSource
{
    FollowTargetType TargetType { get; }

    /// <summary>
    ///     Fetches the target's current projects and display name. Never throws for API errors,
    ///     returns a failed <see cref="FollowSourceResult" /> instead.
    /// </summary>
    Task<FollowSourceResult> GetProjectsAsync(string targetId, CancellationToken ct = default);
}

/// <summary>
///     Result of one source call. <see cref="Projects" /> is null when the call failed;
///     in that case the sync must not remove any data.
/// </summary>
public sealed record FollowSourceResult(IReadOnlyList<Project>? Projects, string? TargetName, string? Error)
{
    public bool Success => Projects is not null;

    public static FollowSourceResult Ok(IReadOnlyList<Project> projects, string targetName) =>
        new(projects, targetName, null);

    public static FollowSourceResult Failed(string error) => new(null, null, error);
}
