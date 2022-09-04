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
    /// <summary>
    /// Gets guild by its ID
    /// </summary>
    /// <param name="guildId"></param>
    /// <returns></returns>
    public Task<Guild?> GetGuildByIdAsync(ulong guildId);
    /// <summary>
    /// Sets the default update channel for the guild
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="defaultUpdateChannel"></param>
    /// <returns></returns>
    public Task SetDefaultUpdateChannelForGuild(ulong guildId, ulong defaultUpdateChannel);

    /// <summary>
    /// Adds Modrinth project to specific guild and generates Modrinth entry for this guild
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="projectId"></param>
    /// <param name="lastCheckVersion">ID of the latest version of this project</param>
    /// <param name="customChannelId">Custom channel, if it should differ from default channel, left null if default is to be used</param>
    /// <param name="projectTitle">Title of the project, not required</param>
    /// <returns>If the operation was successful</returns>
    public Task<bool> AddModrinthProjectToGuildAsync(ulong guildId, string projectId, string lastCheckVersion,
        ulong customChannelId, string? projectTitle = null);
    /// <summary>
    /// Removes Modrinth entry for specific guild and project
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="projectId"></param>
    /// <returns></returns>
    public Task<bool> RemoveModrinthProjectFromGuildAsync(ulong guildId, string projectId);
    /// <summary>
    /// Gets Modrinth project stored in database
    /// </summary>
    /// <param name="projectId"></param>
    /// <returns>Null if the project is not in database</returns>
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
    /// <param name="title">The new title of the project</param>
    /// <param name="lastUpdate">The time of the last update, if null, DateTime.Now() will be used</param>
    /// <returns>True if the update was successful, False if not</returns>
    public Task<bool> UpdateModrinthProjectAsync(string projectId, string? newVersion = null, string? title = null, DateTime? lastUpdate = null);
    
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

    /// <summary>
    /// Gets Modrinth entry for the specific project and guild
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="projectId"></param>
    /// <returns></returns>
    public Task<ModrinthEntry?> GetModrinthEntryAsync(ulong guildId, string projectId);
    /// <summary>
    /// Gets all guilds subscribed to specific project
    /// </summary>
    /// <param name="projectId"></param>
    /// <returns></returns>
    public Task<IList<Guild>> GetAllGuildsSubscribedToProject(string projectId);

    /// <summary>
    /// Saves the ManageRole to provided guild
    /// </summary>
    /// <param name="guildId">ID of the guild</param>
    /// <param name="roleId">ID of the role</param>
    /// <returns>If it was completed successfully</returns>
    public Task<bool> SetManageRoleAsync(ulong guildId, ulong? roleId);

    /// <summary>
    /// Gets the ManageRole from provided guild
    /// </summary>
    /// <param name="guildId"></param>
    /// <returns></returns>
    public Task<ulong?> GetManageRoleIdAsync(ulong guildId);
    /// <summary>
    /// Updates channel for specific Modrinth entry
    /// </summary>
    /// <param name="guildId">ID of the guild</param>
    /// <param name="projectId">ID of the project</param>
    /// <param name="newChannelId">ID of the new channel</param>
    /// <returns></returns>
    public Task<bool> ChangeModrinthEntryChannel(ulong guildId, string projectId, ulong newChannelId);
}