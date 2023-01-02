using System.Data.Entity.Core;
using Asterion.Database;
using Asterion.Database.Models;
using Asterion.Interfaces;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Asterion.Services;

public class DataService : IDataService
{
    private readonly ILogger<DataService> _logger;
    private readonly DiscordSocketClient _client;
    private readonly IServiceProvider _services;
    public DataService(IServiceProvider services, ILogger<DataService> logger, DiscordSocketClient client)
    {
        _services = services;
        _logger = logger;
        _client = client;

        _client.JoinedGuild += JoinGuild;
        _client.LeftGuild += LeaveGuild;
    }
    
    public async Task InitializeAsync()
    {
        await CreateMissingGuildSettingEntries();
        await RegisterNewGuilds();
        await RemoveLeftGuilds();
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

    private async Task CreateMissingGuildSettingEntries()
    {
        _logger.LogDebug("Creating missing guild setting entries");
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        foreach (var guild in db.Guilds)
        {
            var settings = await db.GuildSettings.FirstOrDefaultAsync(x => x.Guild == guild);

            // Setting exists
            if (settings is not null)
            {
                continue;
            }

            var newSettings = db.GuildSettings.Add(new GuildSettings()
            {
                Guild = guild
            });

            guild.Settings = newSettings.Entity;

            await db.SaveChangesAsync();
        }
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

    /// <summary>
    /// Removes guilds that the bot was kicked from while being offline
    /// </summary>
    private async Task RemoveLeftGuilds()
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        
        _logger.LogInformation("Removing guilds that the bot is no longer connected to");

        if (_client.LoginState != LoginState.LoggedIn)
        {
            _logger.LogError("Guild removal interrupted because client is not logged in");
            return;
        }
        
        var removedGuildsCount = 0;
        var connectedGuilds = _client.Guilds;

        if (connectedGuilds is null || connectedGuilds.Any() == false)
        {
            _logger.LogWarning("Guild removal interrupted because connected guilds count is zero");
            return;
        }

        foreach (var guild in db.Guilds)
        {
            var result = connectedGuilds.FirstOrDefault(x => x.Id == guild.GuildId);

            // Asterion is not connected to this guild and we can remove it
            if (result is null)
            {
                _logger.LogInformation("Removing guild ID {ID}", guild.GuildId);
                db.Remove(guild);
                await db.SaveChangesAsync();

                removedGuildsCount++;
            }
        }
        
        _logger.LogInformation("Removed {Count} guilds", removedGuildsCount);
    }

    public async Task<bool> RemoveGuildAsync(ulong guildId)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var guild = db.Guilds.Include(o => o.GuildModrinthEntries).SingleOrDefault(x => x.GuildId == guildId);

        if (guild is null)
        {
            _logger.LogError("No guild with ID {ID} found in database, guild removal interrupted", guildId);
            return false;
        }

        var array = guild.GuildModrinthEntries;
        
        // Remove all subscribed items
        db.ModrinthEntries.RemoveRange(array);
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
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

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
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var guild = await db.Guilds
            .Include(o => o.GuildModrinthEntries)
                .ThenInclude(e => e.Project)
            .Include(g => g.Settings)
            .AsNoTracking()
            .SingleOrDefaultAsync(g => g.GuildId == guildId);

        return guild;
    }

    public async Task<bool> UpdateGuildAsync(Guild updatedGuild)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var guild = await db.Guilds.Include(o => o.Settings).FirstOrDefaultAsync(g => g.GuildId == updatedGuild.GuildId);

        if (guild is null)
        {
            return false;
        }

        guild.ManageRole = updatedGuild.ManageRole;
        guild.PingRole = updatedGuild.PingRole;
        
        guild.Settings.MessageStyle = updatedGuild.Settings.MessageStyle;
        guild.Settings.RemoveOnLeave = updatedGuild.Settings.RemoveOnLeave;
        guild.Settings.ShowChannelSelection = updatedGuild.Settings.ShowChannelSelection;
        guild.Settings.CheckMessagesForModrinthLink = updatedGuild.Settings.CheckMessagesForModrinthLink;
        guild.Settings.ShowSubscribeButton = updatedGuild.Settings.ShowSubscribeButton;

        await db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> AddGuildAsync(ulong guildId)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        // We check if the guild is already added
        var guild = await db.Guilds.FirstOrDefaultAsync(g => g.GuildId == guildId);

        if (guild is not null)
        {
            _logger.LogError("Guild id {Id} is already registered in database", guildId);
            return false;
        }

        var guildSettingsEntry = db.GuildSettings.Add(new GuildSettings());

        var guildEntry = db.Guilds.Add(new Guild()
        {
            GuildId = guildId,
            Settings = guildSettingsEntry.Entity,
            Created = DateTime.Now
        });

        guildSettingsEntry.Entity.Guild = guildEntry.Entity;

        await db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> AddModrinthProjectToGuildAsync(ulong guildId, string projectId, string lastCheckVersion, ulong customChannelId, string? projectTitle = null)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var guild = db.Guilds.Include(o => o.GuildModrinthEntries).FirstOrDefault(x => x.GuildId == guildId);

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
                LastCheckVersion = lastCheckVersion,
                Title = projectTitle
            }).Entity;
        }
        
        db.ModrinthEntries.Add(new GuildModrinthEntry()
        {
            CustomUpdateChannel = customChannelId,
            Created = DateTime.Now,
            Guild = guild,
            Project = project
        });

        await db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> RemoveModrinthProjectFromGuildAsync(ulong guildId, string projectId)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

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
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var project = db.ModrinthProjects.FirstOrDefault(x => x.ProjectId == projectId);
        
        return project;
    }

    public async Task<IList<GuildModrinthEntry>?> GetAllGuildsSubscribedProjectsAsync(ulong guildId)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        
        var guild = await db.Guilds
            .Include(o => o.GuildModrinthEntries)
            .ThenInclude(o => o.Project)
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (guild is null)
        {
            return null;
        }

        var entries = guild.GuildModrinthEntries.ToList();

        return entries;
    }
    
    public async Task<bool> UpdateModrinthProjectAsync(string projectId, string? newVersion = null, string? title = null, DateTime? lastUpdate = null)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        
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
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var projects = db.ModrinthProjects.Select(x => x).ToList();

        return projects;
    }
    
    public async Task<bool> IsGuildSubscribedToProjectAsync(ulong guildId, string projectId)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var guildsProjects = await GetAllGuildsSubscribedProjectsAsync(guildId);

        if (guildsProjects is null)
        {
            _logger.LogError("Guild with ID {GuildId} does not exist", guildId);
            throw new ObjectNotFoundException($"Guild with ID {guildId} does not exist");
        }

        var entry = guildsProjects.FirstOrDefault(x => x.ProjectId == projectId && x.GuildId == guildId);

        return entry is not null;
    }

    public async Task<GuildModrinthEntry?> GetModrinthEntryAsync(ulong guildId, string projectId)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var entry = db.ModrinthEntries.FirstOrDefault(x => x.GuildId == guildId && x.ProjectId == projectId);

        return entry;
    }

    public async Task<IList<Guild>> GetAllGuildsSubscribedToProject(string projectId)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var guilds = db.ModrinthEntries
            .Include(o => o.Guild)
            .ThenInclude(o => o.Settings)
            .Where(x => x.ProjectId == projectId)
            .Select(x => x.Guild).ToList();

        return guilds;
    }

    public async Task<bool> SetManageRoleAsync(ulong guildId, ulong? roleId)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

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
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var guild = await GetGuildByIdAsync(guildId);

        // Return null if guild does not exists, otherwise return ManageRole value
        return guild?.ManageRole;
    }

    public async Task<bool> ChangeModrinthEntryChannel(ulong guildId, string projectId, ulong newChannelId)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var guild = await GetGuildByIdAsync(guildId);

        if (guild is null)
        {
            return false;
        }

        // Get entry tracked by EF
        var entry = db.ModrinthEntries.FirstOrDefault(x => x.GuildId == guildId && x.ProjectId == projectId);

        if (entry is null)
        {
            return false;
        }

        entry.CustomUpdateChannel = newChannelId;

        await db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> SetPingRoleAsync(ulong guildId, ulong? roleId)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var guild = db.Guilds.SingleOrDefault(x => x.GuildId == guildId);

        if (guild is null)
        {
            return false;
        }

        guild.PingRole = roleId;

        await db.SaveChangesAsync();

        return true;
    }

    public async Task<ulong?> GetPingRoleIdAsync(ulong guildId)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var guild = await GetGuildByIdAsync(guildId);

        // Return null if guild does not exists, otherwise return ManageRole value
        return guild?.PingRole;
    }
}