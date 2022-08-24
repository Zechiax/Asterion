using RinthBot.Database;

namespace RinthBot.Interfaces;

public interface IDataService
{
    /// <summary>
    /// Initialization, should be called after client is ready
    /// </summary>
    public Task InitializeAsync();
    
    /// <summary>
    /// Adds a guild to database with default values
    /// </summary>
    /// <param name="guildId">ID of the guild to be added</param>
    /// <returns>True if the guild was added, false if the guild was already in the database</returns>
    public Task<bool> AddGuildAsync(ulong guildId);
    /// <summary>
    /// Removes guild from database
    /// </summary>
    /// <param name="guildId">ID of the guild to be removed</param>
    /// <returns>True if the guild was removed, false if the removal failed; guild doesn't exists</returns>
    public Task<bool> RemoveGuildAsync(ulong guildId);
    public Task<Guild?> GetGuildByIdAsync(ulong guildId);
    public Task SetDefaultUpdateChannelForGuild(ulong guildId, ulong defaultUpdateChannel);
    public Task<bool> AddModrinthProjectToGuildAsync(ulong guildId, string projectId, string lastCheckVersion,
        ulong? customChannelId);

    public Task<bool> RemoveModrinthProjectFromGuildAsync(ulong guildId, string projectId);
    public Task<ModrinthProject?> GetModrinthProjectByIdAsync(string projectId);
    /// <summary>
    /// Returns all subscribed projects of the guild
    /// </summary>
    /// <param name="guildId">The guild's ID</param>
    /// <returns></returns>
    public Task<IList<ModrinthEntry>?> GetAllGuildsSubscribedProjectsAsync(ulong guildId);
    
    /// <summary>
    /// Updates Modrinth project with new information
    /// </summary>
    /// <param name="projectId">The ID of the project to be updated</param>
    /// <param name="newVersion">The new version string</param>
    /// <param name="lastUpdate">The time of the last update, if null, DateTime.Now() will be used</param>
    /// <returns>True if the update was successful, False if not</returns>
    public Task<bool> UpdateModrinthProjectAsync(string projectId, string newVersion, DateTime? lastUpdate = null);
    
    /// <summary>
    /// Returns all Modrinth projects stored in database
    /// </summary>
    /// <returns></returns>
    public Task<IList<ModrinthProject>> GetAllModrinthProjectsAsync();
    
    /// <summary>
    /// Finds if the guild is subscribed to provided project or not
    /// </summary>
    /// <param name="guildId">The guild's ID</param>
    /// <param name="projectId">The project's ID</param>
    /// <returns>True if guild is subscribed to project, false if not</returns>
    public Task<bool> IsGuildSubscribedToProjectAsync(ulong guildId, string projectId);

    public Task<ModrinthEntry?> GetModrinthEntryAsync(ulong guildId, string projectId);
    public Task<IList<Guild>> GetAllGuildsSubscribedToProject(string projectId);
}