using Asterion.Database.Models;

namespace Asterion.Interfaces;

/// <summary>
///     Projects a guild subscribed to manually, stored as <see cref="ModrinthEntry" />
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    ///     Subscribes the guild to the project, adds the shared project state if it is not stored yet
    /// </summary>
    /// <param name="lastCheckVersion">ID of the latest version of the project</param>
    /// <param name="channelId">Channel the updates are sent to</param>
    /// <returns>False if the guild is not in the database</returns>
    public Task<bool> AddSubscriptionAsync(ulong guildId, string projectId, string lastCheckVersion,
        DateTime lastUpdated, ulong channelId, string? projectTitle = null, CancellationToken ct = default);

    /// <summary>
    ///     Removes the subscription and the project if nothing else uses it
    /// </summary>
    /// <returns>The removed subscription with its project, null if the guild is not subscribed to it</returns>
    public Task<ModrinthEntry?> RemoveSubscriptionAsync(ulong guildId, string projectId,
        CancellationToken ct = default);

    /// <summary>
    ///     Removes every subscription of the guild and every project only they were using
    /// </summary>
    /// <returns>Number of removed subscriptions</returns>
    public Task<int> RemoveAllSubscriptionsAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    ///     Subscriptions of the guild with their projects
    /// </summary>
    public Task<IList<ModrinthEntry>> GetSubscriptionsAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    ///     The guild's subscription with its project, null if the guild is not subscribed to it
    /// </summary>
    public Task<ModrinthEntry?> GetSubscriptionAsync(ulong guildId, string projectId, CancellationToken ct = default);

    public Task<bool> IsSubscribedAsync(ulong guildId, string projectId, CancellationToken ct = default);

    /// <summary>
    ///     Applies the change to the guild's subscription and saves it
    /// </summary>
    /// <returns>The saved subscription with its project, null if the guild is not subscribed to it</returns>
    public Task<ModrinthEntry?> UpdateSubscriptionAsync(ulong guildId, string projectId, Action<ModrinthEntry> update,
        CancellationToken ct = default);
}
