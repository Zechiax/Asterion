using Asterion.Database.Models;

namespace Asterion.Interfaces;

public interface IGuildService
{
    /// <summary>
    ///     Adds a guild to database with default values
    /// </summary>
    /// <returns>True if the guild was added, false if the guild was already in the database</returns>
    public Task<bool> AddGuildAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    ///     Removes the guild with its subscriptions and follows, and every project only they were using
    /// </summary>
    /// <returns>False if the guild is not in the database</returns>
    public Task<bool> RemoveGuildAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    ///     The guild with its settings, null if it is not in the database
    /// </summary>
    public Task<Guild?> GetGuildAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    ///     Applies the change to the guild and its settings and saves it
    /// </summary>
    /// <returns>The saved guild, null if it is not in the database</returns>
    public Task<Guild?> UpdateGuildAsync(ulong guildId, Action<Guild> update, CancellationToken ct = default);

    /// <returns>ID of the manage role, null if it has not been set or the guild doesn't exist</returns>
    public Task<ulong?> GetManageRoleIdAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    ///     If the server has milestone messages turned on in its settings
    /// </summary>
    public Task<bool> MilestonesEnabledAsync(ulong guildId, CancellationToken ct = default);
}
