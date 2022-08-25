using System.Data.Entity.Core;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RinthBot.Database;
using RinthBot.Interfaces;

namespace RinthBot.Services;

public class DataService : IDataService
{
    private readonly ILogger<DataService> _logger;
    private readonly DiscordSocketClient _client;
    public DataService(ILogger<DataService> logger, DiscordSocketClient client)
    {
        _logger = logger;
        _client = client;

        _client.JoinedGuild += JoinGuild;
        _client.LeftGuild += LeaveGuild;
    }
    
    public async Task InitializeAsync()
    {
        await RegisterNewGuilds();
        
        // Clean entries with no project
        await RemoveProjectsWithNoEntries();
    }

    private async Task JoinGuild(SocketGuild guild)
    {
        await AddGuildAsync(guild.Id);
    }

    private async Task LeaveGuild(SocketGuild guild)
    {
        await RemoveGuildAsync(guild.Id);
    }

    /// <summary>
    /// Registers new guilds to which the bot has been invited while offline
    /// </summary>
    private async Task RegisterNewGuilds()
    {
        _logger.LogInformation("Registering new guilds");

        var newGuildsCount = 0;
        var guilds = _client.Guilds;

        if (guilds is null)
        {
            _logger.LogError("Could not get list of connected guilds");
            return;
        }

        foreach (var guild in guilds)
        {
            var info = await GetGuildByIdAsync(guild.Id);

            // Guild is already registered
            if (info is not null)
            {
                continue;
            }
            
            // Guild is not registered, we have to add guild

            newGuildsCount++;
            _logger.LogInformation("New guild id {Id} found, registering", guild.Id);
            await AddGuildAsync(guild.Id);
        }
        
        _logger.LogInformation("Registered {Count} new guilds", newGuildsCount);
    }

    private static DataContext GetDbContext()
    {
        return new DataContext();
    }

    public async Task<bool> RemoveGuildAsync(ulong guildId)
    {
        await using var db = GetDbContext();

        var guild = db.Guilds.Include(o => o.ModrinthArray).SingleOrDefault(x => x.GuildId == guildId);

        if (guild is null)
        {
            _logger.LogError("No guild with ID {ID} found in database, guild removal interrupted", guildId);
            return false;
        }

        var array = guild.ModrinthArray;
        
        // Remove all subscribed items
        db.ModrinthEntries.RemoveRange(db.ModrinthEntries.Where(x => x.ArrayId == array.ArrayId));
        // Remove the array
        db.Arrays.Remove(array);
        // Remove the guild
        db.Guilds.Remove(guild);

        await db.SaveChangesAsync();

        // Remove all unused projects
        await RemoveProjectsWithNoEntries();
        
        return true;
    }

    private async Task RemoveProjectsWithNoEntries()
    {
        _logger.LogInformation("Removing projects with no entries");
        await using var db = GetDbContext();

        var projects = db.ModrinthProjects.ToList();
        
        foreach (var project in projects)
        {
            var entries = db.ModrinthEntries.Where(x => x.Project == project);

            
            if (entries.Any()) continue;
            
            // If the project has no entries, there is no need for it in database
            _logger.LogInformation("Project ID {ProjectId} {ProjectTitle} has no entries, it\'s being removed", project.ProjectId, project.Title);
            db.ModrinthProjects.Remove(project);
            await db.SaveChangesAsync();
        }
    }

    public async Task<Guild?> GetGuildByIdAsync(ulong guildId)
    {
        await using var db = GetDbContext();

        var guild = await db.Guilds.FirstOrDefaultAsync(g => g.GuildId == guildId);

        return guild;
    }

    public async Task SetDefaultUpdateChannelForGuild(ulong guildId, ulong defaultUpdateChannel)
    {
        await using var db = GetDbContext();

        var guild = db.Guilds.SingleOrDefault(x => x.GuildId == guildId);

        if (guild is null)
        {
            throw new ObjectNotFoundException();
        }

        guild.UpdateChannel = defaultUpdateChannel;

        await db.SaveChangesAsync();
    }

    public async Task<bool> AddGuildAsync(ulong guildId)
    {
        await using var db = GetDbContext();

        // We check if the guild is already added
        var guild = await db.Guilds.FirstOrDefaultAsync(g => g.GuildId == guildId);

        if (guild is not null)
        {
            _logger.LogError("Guild id {Id} is already registered in database", guildId);
            return false;
        }

        var arrayEntry = db.Arrays.Add(new Database.Array()
            {
                Type = ArrayType.Modrinth
            }
        );

        var guildEntry = db.Guilds.Add(new Guild()
        {
            GuildId = guildId,
            ModrinthArray = arrayEntry.Entity,
            Created = DateTime.Now
        });

        arrayEntry.Entity.Guild = guildEntry.Entity;

        await db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> AddModrinthProjectToGuildAsync(ulong guildId, string projectId, string lastCheckVersion, ulong? customChannelId = null)
    {
        await using var db = GetDbContext();

        var guild = db.Guilds.Include(o => o.ModrinthArray).FirstOrDefault(x => x.GuildId == guildId);

        if (guild is null)
        {
            _logger.LogError("No guild with ID {ID}", guildId);
            return false;
        }

        // Let's find out if the project is already in the database
        var project = db.ModrinthProjects.FirstOrDefault(x => x.ProjectId == projectId);

        // Project is not in database, we have to add it
        if (project is null)
        {
            project = db.ModrinthProjects.Add(new ModrinthProject()
            {
                ProjectId = projectId,
                Created = DateTime.Now,
                LastUpdated = DateTime.Now,
                LastCheckVersion = lastCheckVersion
            }).Entity;
        }
        
        db.ModrinthEntries.Add(new ModrinthEntry()
        {
            CustomUpdateChannel = customChannelId,
            Created = DateTime.Now,
            Guild = guild,
            Project = project,
            Array = guild.ModrinthArray
        });

        await db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> RemoveModrinthProjectFromGuildAsync(ulong guildId, string projectId)
    {
        await using var db = GetDbContext();

        var entry = await GetModrinthEntryAsync(guildId, projectId);

        if (entry is null)
        {
            return false;
        }

        db.ModrinthEntries.Remove(entry);

        await db.SaveChangesAsync();

        var guilds = await GetAllGuildsSubscribedToProject(projectId);

        // No other guild is subscribed to this project, we can remove it
        if (guilds.Count == 0)
        {
            var project = db.ModrinthProjects.Single(x => x.ProjectId == projectId);

            db.Remove(project);

            await db.SaveChangesAsync();
        }

        return true;
    }

    public async Task<ModrinthProject?> GetModrinthProjectByIdAsync(string projectId)
    {
        await using var db = GetDbContext();

        var project = db.ModrinthProjects.FirstOrDefault(x => x.ProjectId == projectId);
        
        return project;
    }

    public async Task<IList<ModrinthEntry>?> GetAllGuildsSubscribedProjectsAsync(ulong guildId)
    {
        await using var db = GetDbContext();
        
        var guild = await GetGuildByIdAsync(guildId);

        if (guild is null)
        {
            return null;
        }

        var entries = db.ModrinthEntries.Include(o => o.Project).Where(x => x.ArrayId == guild.ModrinthArrayId).ToList();

        return entries;
    }
    
    public async Task<bool> UpdateModrinthProjectAsync(string projectId, string? newVersion = null, string? title = null, DateTime? lastUpdate = null)
    {
        await using var db = GetDbContext();
        
        lastUpdate ??= DateTime.Now;

        var project = db.ModrinthProjects.SingleOrDefault(x => x.ProjectId == projectId);

        if (project is null)
        {
            return false;
        }

        project.LastUpdated = lastUpdate;

        // Update version
        if (string.IsNullOrEmpty(newVersion) == false)
        {
            project.LastCheckVersion = newVersion;
        }

        // Update project title
        if (string.IsNullOrEmpty(title) == false)
        {
            project.Title = title;
        }

        await db.SaveChangesAsync();

        return true;
    }
    
    public async Task<IList<ModrinthProject>> GetAllModrinthProjectsAsync()
    {
        await using var db = GetDbContext();

        var projects = db.ModrinthProjects.Select(x => x).ToList();

        return projects;
    }
    
    public async Task<bool> IsGuildSubscribedToProjectAsync(ulong guildId, string projectId)
    {
        await using var db = GetDbContext();

        var guildsProjects = await GetAllGuildsSubscribedProjectsAsync(guildId);

        if (guildsProjects is null)
        {
            _logger.LogError("Guild with ID {GuildId} does not exist", guildId);
            throw new ObjectNotFoundException($"Guild with ID {guildId} does not exist");
        }

        var entry = guildsProjects.FirstOrDefault(x => x.ProjectId == projectId && x.GuildId == guildId);

        return entry is not null;
    }

    public async Task<ModrinthEntry?> GetModrinthEntryAsync(ulong guildId, string projectId)
    {
        await using var db = GetDbContext();

        var entry = db.ModrinthEntries.FirstOrDefault(x => x.GuildId == guildId && x.ProjectId == projectId);

        return entry;
    }

    public async Task<IList<Guild>> GetAllGuildsSubscribedToProject(string projectId)
    {
        await using var db = GetDbContext();

        var guilds = db.ModrinthEntries.Include(o => o.Guild).Where(x => x.ProjectId == projectId)
            .Select(x => x.Guild).ToList();

        return guilds;
    }

    public async Task<bool> SetManageRoleAsync(ulong guildId, ulong? roleId)
    {
        await using var db = GetDbContext();

        var guild = db.Guilds.SingleOrDefault(x => x.GuildId == guildId);

        if (guild is null)
        {
            return false;
        }

        guild.ManageRole = roleId;

        await db.SaveChangesAsync();

        return true;
    }

    public async Task<ulong?> GetManageRoleIdAsync(ulong guildId)
    {
        await using var db = GetDbContext();

        var guild = await GetGuildByIdAsync(guildId);

        // Return null if guild does not exists, otherwise return ManageRole value
        return guild?.ManageRole;
    }
}