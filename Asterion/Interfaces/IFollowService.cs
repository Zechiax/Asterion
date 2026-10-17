using Asterion.Database.Models;
using Asterion.Services.Follows;

namespace Asterion.Interfaces;

public interface IFollowService
{
    /// <summary>Maximum number of follows per guild</summary>
    public const int MaxFollowsPerGuild = 500;

    /// <summary>
    ///     Creates a follow and runs its first sync without announcing anything
    /// </summary>
    /// <param name="newProjectChannelId">Channel for new project messages, null to use <paramref name="channelId" /></param>
    public Task<AddFollowResult> AddFollowAsync(ulong guildId, FollowTargetType targetType, string targetId,
        string targetName, ulong? channelId, FollowEvents events, ulong? newProjectChannelId = null,
        CancellationToken ct = default);

    /// <summary>
    ///     Removes the follow and every project only it was using
    /// </summary>
    /// <returns>False if the guild has no such follow</returns>
    public Task<bool> RemoveFollowAsync(ulong guildId, int followId, CancellationToken ct = default);

    public Task<bool> SetIgnoredAsync(ulong guildId, int followId, string projectId, bool ignored,
        CancellationToken ct = default);

    public Task<IList<ModrinthFollow>> GetFollowsAsync(ulong guildId, CancellationToken ct = default);

    public Task<ModrinthFollow?> GetFollowAsync(ulong guildId, int followId, CancellationToken ct = default);

    public Task<ModrinthFollow?> UpdateSettingsAsync(ulong guildId, int followId, Action<ModrinthFollow> update,
        CancellationToken ct = default);

    /// <summary>
    ///     Returns every follow of every guild, without related data
    /// </summary>
    public Task<IList<ModrinthFollow>> GetAllFollowsAsync(CancellationToken ct = default);

    public Task<FollowSourceResult> FetchTargetAsync(FollowTargetType targetType, string targetId,
        CancellationToken ct = default);

    public Task<SyncResult?> SyncFollowAsync(int followId, bool announceNew, CancellationToken ct = default);

    /// <summary>
    ///     Syncs the follow against an already fetched source result, so follows of the same target
    ///     can share one source call
    /// </summary>
    /// <returns>Null if the follow does not exist</returns>
    public Task<SyncResult?> SyncFollowAsync(int followId, FollowSourceResult source, bool announceNew,
        CancellationToken ct = default);
}

public enum AddFollowStatus
{
    Added,
    AlreadyFollowing,
    LimitReached,
    GuildNotFound
}

/// <param name="Status">Outcome of the add</param>
/// <param name="Follow">The created follow, null unless <paramref name="Status" /> is Added</param>
/// <param name="Sync">Result of the first sync, null unless <paramref name="Status" /> is Added</param>
public sealed record AddFollowResult(AddFollowStatus Status, ModrinthFollow? Follow = null, SyncResult? Sync = null);

/// <param name="Success">False if the source call failed, nothing was changed then</param>
/// <param name="Error">Error of the failed source call</param>
/// <param name="TrackedCount">Number of followed projects after the sync</param>
/// <param name="Added">Projects added by this sync</param>
/// <param name="Removed">Projects removed by this sync</param>
/// <param name="Announced">Projects announced as new by this sync</param>
public sealed record SyncResult(
    bool Success,
    string? Error,
    int TrackedCount,
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Removed,
    IReadOnlyList<string> Announced);
