using System.ComponentModel;
using System.Net;
using System.Timers;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modrinth.RestClient;
using Modrinth.RestClient.Models;
using RestEase;
using RinthBot.ComponentBuilders;
using RinthBot.Database;
using Timer = System.Timers.Timer;
using Version = Modrinth.RestClient.Models.Version;
using RinthBot.EmbedBuilders;
using RinthBot.Interfaces;

namespace RinthBot.Services;

public class UpdateDto
{
    public Version[]? Versions { get; set; }
    public Project? Project { get; set; }
    public TeamMember[]? TeamMembers { get; set; }

    /// <summary>
    /// True if every information has been set, false otherwise
    /// </summary>
    public bool Successful => Versions is not null && Project is not null && TeamMembers is not null;
}

public class ModrinthService
{
    private readonly BackgroundWorker _updateWorker;
    private readonly IModrinthApi _api;
    private readonly ILogger _logger;
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _cacheEntryOptions;
    private readonly IDataService _dataService;
    private readonly DiscordSocketClient _client;
    
    public ModrinthService(IServiceProvider serviceProvider)
    {
        _api = ModrinthApi.NewClient(userAgent: "RinthBot");
        _logger = serviceProvider.GetRequiredService<ILogger<ModrinthService>>();
        _cache = serviceProvider.GetRequiredService<IMemoryCache>();
        _dataService = serviceProvider.GetRequiredService<DataService>();
        _client = serviceProvider.GetRequiredService<DiscordSocketClient>();
        
        _cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        };
        
        _updateWorker = new BackgroundWorker();
        _updateWorker.DoWork += CheckUpdates;
        
        
        var checkTimer = new Timer(MinutesToMilliseconds(60));
        checkTimer.Elapsed += checkTimer_Elapsed;
        checkTimer.Start();
        
        _logger.LogInformation("Modrinth service initialized");
    }
    private static double MinutesToMilliseconds(int minutes)
    {
        return TimeSpan.FromMinutes(minutes).TotalMilliseconds;
    }

    /// <summary>
    /// Checks updates for every project stored in database, sends notification to every guild who has subscribed for updates  
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void CheckUpdates(object? sender, DoWorkEventArgs e)
    {
        _logger.LogInformation("Running update check");
        var projects = await _dataService.GetAllModrinthProjectsAsync();

        List<ulong> notifiedGuilds = new();

        // Check every project for update sequentially (lesser chance for being rate-limited)
        foreach (var project in projects)
        {
            _logger.LogInformation("Checking project ID {ProjectID}", project.ProjectId);
            try
            {
                var updateInfo = await GetProjectUpdateInfo(project.ProjectId);

                if (updateInfo.Successful == false)
                {
                    _logger.LogError("Updater wasn't able to get information for project ID {ID}, skipping...", project.ProjectId);
                    continue;
                }
                
                // Update title of the project in the database
                if (updateInfo.Project is not null && project.Title != updateInfo.Project.Title)
                {
                    _logger.LogInformation("Updating title of project ID {ID} from title '{OldTitle}' to '{NewTitle}'", project.ProjectId, project.Title, updateInfo.Project.Title);
                    await _dataService.UpdateModrinthProjectAsync(project.ProjectId, title: updateInfo.Project.Title);
                }

                if (updateInfo.Versions!.Length < 1)
                {
                    _logger.LogInformation("No new versions for project ID {ID}", project.ProjectId);
                    continue;
                }

                _logger.LogInformation("Found {Count} new Versions", updateInfo.Versions!.Length);
                
                var guilds = await _dataService.GetAllGuildsSubscribedToProject(project.ProjectId);

                foreach (var guild in guilds)
                {
                    var channel = await GetGuildUpdateChannel(guild, project.ProjectId);

                    // Channel is not set, skip sending updates to this guild
                    if (channel is null)
                    {
                        _logger.LogInformation("Guild ID {GuildID} has not yet set default update channel or custom channel for this project", guild.GuildId);
                        var socketGuild = _client.GetGuild(guild.GuildId);

                        if (socketGuild is not null && notifiedGuilds.Contains(guild.GuildId) == false)
                        {
                            _logger.LogInformation("Sending information message to the owner of this guild");
                            notifiedGuilds.Add(guild.GuildId);
                            await InformOwner(socketGuild, updateInfo.Project!);
                        }

                        continue;
                    }
                    
                    _logger.LogInformation("Sending updates to guild ID {Id} and channel ID {Channel}", guild.GuildId, channel.Id);
                    
                    // None of these can be null, everything is checked beforehand
                    await SendUpdatesToChannel(channel, updateInfo.Project!, updateInfo.Versions!, updateInfo.TeamMembers!);
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Exception while checking for update for project ID {ID}; Exception: {Exception}", project.ProjectId, ex.Message);
            }
        }
        _logger.LogInformation("Update check ended");
    }

    private async Task InformOwner(SocketGuild guild, Project project)
    {
        await guild.Owner.SendMessageAsync(
            $"Hi! I've found updates for one of your subscribed projects ({project.Id} - {project.Title}), but due to changes on how subscribing project works, this project has no update channel set" +
            $"\n\nPlease use `/modrinth change-channel [projectId] [newChannel]` command, you can check the documentation for this command here: https://zechiax.gitbook.io/rinthbot/commands/change-channel" +
            $"\n\nFor more information regarding subscribing projects, see this guide https://zechiax.gitbook.io/rinthbot/guides/subscribe-to-your-first-project");
    }

    private async Task SendUpdatesToChannel(SocketTextChannel textChannel, Project currentProject, IEnumerable<Version> newVersions, TeamMember[] team)
    {
        // Iterate versions - they are ordered from latest to oldest, we want to sent them chronologically
        foreach (var version in newVersions.Reverse())
        {
            var embed = ModrinthEmbedBuilder.VersionUpdateEmbed(currentProject, version, team);
            var buttons =
                new ComponentBuilder().WithButton(
                    ModrinthComponentBuilder.GetVersionUrlButton(currentProject, version));
            try
            {
                await textChannel.SendMessageAsync(embed: embed.Build(), components: buttons.Build());
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Error while sending message to guild {Guild}: {Exception}", textChannel.Guild.Id, ex.Message);
            }
        }
    }

    private async Task<SocketTextChannel?> GetGuildUpdateChannel(Guild guild, string projectId)
    {
        var projectInfo = await _dataService.GetModrinthEntryAsync(guild.GuildId, projectId);

        SocketTextChannel? channel = null;
                
        // We check with Discord if the channel exists or has been deleted (returns null if so)
        
        // Custom channel is not set
        if (projectInfo?.CustomUpdateChannel == null)
        {
            // Default channel is set
            if (guild.UpdateChannel != null)
            {
                channel = _client.GetGuild(guild.GuildId).GetTextChannel((ulong)guild.UpdateChannel);
            }
        }
        // Custom channel is set
        else
        {
            channel = _client.GetGuild(guild.GuildId).GetTextChannel((ulong)projectInfo.CustomUpdateChannel);
        }

        return channel;
    }

    private async Task<UpdateDto> GetProjectUpdateInfo(string projectId)
    {
        var updateDto = new UpdateDto();

        var retryCounter = 1;
        
        while ((updateDto.Project is null || updateDto.Versions is null) && retryCounter < 4)
        {
            try
            {
                // If project is null, retrieve information from API
                updateDto.Project ??= await GetProject(projectId);

                // Get new versions from API
                updateDto.Versions ??= await CheckProjectForUpdates(projectId);

                // Get project's team
                updateDto.TeamMembers ??= await GetProjectsTeamMembersAsync(projectId);
            }
            // Rate-limiting
            catch(ApiException e) when (e.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // Wait 1 minute before re-trying to get the information again
                _logger.LogInformation("Rate-limited (try #{RetryCount}): Waiting 1 minute before re-trying for project ID {ID}", retryCounter, projectId);
                await Task.Delay(60000);
                retryCounter++;                
            }
        }

        return updateDto;
    }

    /// <summary>
    /// Force check for updates, used for debugging
    /// </summary>
    /// <returns>False if worker is busy</returns>
    public bool ForceUpdate()
    {
        if (_updateWorker.IsBusy)
        {
            return false;
        }
        
        _updateWorker.RunWorkerAsync();

        return true;
    }
    

    private void checkTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (!_updateWorker.IsBusy) _updateWorker.RunWorkerAsync();
    }

    /// <summary>
    /// Checks for new versions and updates the data in the database
    /// </summary>
    /// <param name="projectId"></param>
    /// <returns>List of new versions</returns>
    private async Task<Version[]?> CheckProjectForUpdates(string projectId)
    {
        var versionList = await GetVersionListAsync(projectId);
        var oldProject = await _dataService.GetModrinthProjectByIdAsync(projectId);

        if (versionList is null)
        {
            _logger.LogError("Could not get data from Modrinth for project ID {ID}", projectId);
            return null;
        }
        if (oldProject is null)
        {
            _logger.LogError("Data for project ID {ID} are not present in database", projectId);
            return null;
        }
        
        // Ensures the data is chronologically ordered
        var orderedVersions = versionList.OrderByDescending(x => x.DatePublished);

        // Take new versions from the latest to the one we already checked
        var newVersions = orderedVersions.TakeWhile(version => version.Id != oldProject.LastCheckVersion).ToArray();

        // Update data in database, if there are new releases
        if (newVersions.Length > 0)
        {
            await _dataService.UpdateModrinthProjectAsync(projectId, newVersions[0].Id);
        }

        return newVersions;
        }

    /// <summary>
    /// Gets project by it's slug or ID, firstly it tries to retrieve it from cache, if it's not in cache,
    /// then it will use Modrinth's API and saves the result to cache
    /// </summary>
    /// <param name="slugOrId"></param>
    /// <returns></returns>
    public async Task<Project?> GetProject(string slugOrId)
    {
        if (_cache.TryGetValue(slugOrId, out var value) && value is Project project)
        {
            _logger.LogDebug("{Id} retrieved from cache", slugOrId);
            return project;
        }

        _logger.LogDebug("{Id} not in cache", slugOrId);
        Project? p;
        try
        {
            p = await _api.GetProjectAsync(slugOrId);
        }
        catch (Exception)
        {
            return null;
        }
        
        _cache.Set(slugOrId, p, _cacheEntryOptions);

        return p;
    }

    public async Task<Version[]?> GetVersionListAsync(string slugOrId)
    {
        try
        {
            var searchResponse = await _api.GetProjectVersionListAsync(slugOrId);
            return searchResponse;
        }
        catch (Exception e)
        {
            _logger.LogInformation("{ExceptionMessage}", e.Message);
        }

        return null;
    }

    public async Task<TeamMember[]?> GetProjectsTeamMembersAsync(string projectId)
    {
        try
        {
            var team = await _api.GetProjectTeamMembersByProjectAsync(projectId);
            return team;
        }
        catch (Exception e)
        {
            _logger.LogInformation("{ExceptionMessage}", e.Message);
        }

        return null;
        
    }

    public async Task<SearchResponse?> SearchProjects(string query)
    {
        try
        {
            var searchResponse = await _api.SearchProjectsAsync(query);
            return searchResponse;
        }
        catch (Exception e)
        {
            _logger.LogInformation("{ExceptionMessage}", e.Message);
        }

        return null;
    }

    public async Task<Project[]?> GetMultipleProjects(IEnumerable<string> projectIds)
    {
        try
        {
            var searchResponse = await _api.GetMultipleProjectsAsync(projectIds);
            return searchResponse;
        }
        catch (Exception e)
        {
            _logger.LogInformation("{ExceptionMessage}", e.Message);
        }

        return null;
    }

    public async Task<Version?> GetProjectsLatestVersion(string projectId)
    {
        var versions = await GetVersionListAsync(projectId);

        // Get last version ID
        var lastVersion = versions?.OrderByDescending(x => x.DatePublished).First();

        return lastVersion;
    }
    public async Task<Version?> GetProjectsLatestVersion(Project project)
    {
        return await GetProjectsLatestVersion(project.Id);
    }
}