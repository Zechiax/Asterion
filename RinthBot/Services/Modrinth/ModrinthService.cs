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
using RinthBot.EmbedBuilders;
using RinthBot.Interfaces;
using Version = Modrinth.RestClient.Models.Version;
using Timer = System.Timers.Timer;

namespace RinthBot.Services.Modrinth;

public class UpdateDto
{
    public Version[]? Versions { get; set; }
    public Project? Project { get; set; }
    public TeamMember[]? TeamMembers { get; set; }

    /// <summary>
    /// True if every required information has been set, false otherwise
    /// </summary>
    public bool Successful => Versions is not null && Project is not null;
}

public partial class ModrinthService
{
    private readonly BackgroundWorker _updateWorker;
    private readonly IModrinthApi _api;
    private readonly ILogger _logger;
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _cacheEntryOptions;
    private readonly IDataService _dataService;
    private readonly DiscordSocketClient _client;

    // Temporary solution for sending notifications to guilds with no channel set
    private List<ulong> _notifiedGuilds = new();

    public ModrinthService(IServiceProvider serviceProvider)
    {
        _api = ModrinthApi.NewClient(userAgent: "RinthBot");
        _logger = serviceProvider.GetRequiredService<ILogger<ModrinthService>>();
        _cache = serviceProvider.GetRequiredService<IMemoryCache>();
        _dataService = serviceProvider.GetRequiredService<IDataService>();
        _client = serviceProvider.GetRequiredService<DiscordSocketClient>();
        
        _cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
        };
        
        _updateWorker = new BackgroundWorker();
        _updateWorker.DoWork += CheckUpdates;
        
        
        var checkTimer = new Timer(MinutesToMilliseconds(45));
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

                await CheckGuilds(updateInfo, project, guilds);
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Exception while checking for update for project ID {ID}; Exception: {Exception}", project.ProjectId, ex.Message);
            }
        }
        _logger.LogInformation("Update check ended");
    }

    private async Task CheckGuilds(UpdateDto updateInfo, ModrinthProject project, IEnumerable<Guild> guilds)
    {
        foreach (var guild in guilds)
        {
            var entry = await _dataService.GetModrinthEntryAsync(guild.GuildId, project.ProjectId);

            // Channel is not set, skip sending updates to this guild
            if (entry!.CustomUpdateChannel is null)
            {
                _logger.LogInformation("Guild ID {GuildID} has not yet set default update channel or custom channel for this project", guild.GuildId);
                var socketGuild = _client.GetGuild(guild.GuildId);

                if (socketGuild is not null && _notifiedGuilds.Contains(guild.GuildId) == false)
                {
                    _logger.LogInformation("Sending information message to the owner of this guild");
                    _notifiedGuilds.Add(guild.GuildId);
                    await InformOwner(socketGuild, updateInfo.Project!);
                }

                continue;
            }

            var channel = _client.GetGuild(guild.GuildId).GetTextChannel((ulong)entry.CustomUpdateChannel);
                    
            _logger.LogInformation("Sending updates to guild ID {Id} and channel ID {Channel}", guild.GuildId, channel.Id);
                    
            // None of these can be null, everything is checked beforehand
            await SendUpdatesToChannel(channel, updateInfo.Project!, updateInfo.Versions!, updateInfo.TeamMembers);
        }
    }

    private static async Task InformOwner(SocketGuild guild, Project project)
    {
        await guild.Owner.SendMessageAsync(
            $"Hi! I've found updates for one of your subscribed projects ({project.Id} - {project.Title}), but due to changes on how subscribing project works, this project has no update channel set" +
            $"\n\nPlease use `/change-channel [projectId] [newChannel]` command, you can check the documentation for this command here: https://zechiax.gitbook.io/rinthbot/commands/change-channel" +
            $"\n\nFor more information regarding subscribing projects, see this guide https://zechiax.gitbook.io/rinthbot/guides/subscribe-to-your-first-project");
    }

    /// <summary>
    /// Sends update information about every new version to specified Text Channel
    /// </summary>
    /// <param name="textChannel"></param>
    /// <param name="currentProject"></param>
    /// <param name="newVersions"></param>
    /// <param name="team"></param>
    private async Task SendUpdatesToChannel(SocketTextChannel textChannel, Project currentProject, IEnumerable<Version> newVersions, TeamMember[]? team)
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

                // Get new versions from API, but only search if we have required information, as we wouldn't have send any updates
                if (updateDto.Project is not null)
                {
                    updateDto.Versions ??= await CheckProjectForUpdates(projectId);
                }

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
    /// Tries to find project on Modrinth
    /// </summary>
    /// <param name="query">Either project's slug, ID or general query</param>
    /// <returns></returns>
    public async Task<SearchResult<Project>> FindProject(string query)
    {
        Project? project = null;

        // Slug or ID can't contain space
        if (!query.Contains(' '))
        {
            var projectFoundById = false;
            try
            {
                project = await _api.GetProjectAsync(query);

                // Won't be set if exception is thrown
                projectFoundById = true;
            }
            // Not found status code is returned when requested project was not found
            catch (ApiException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                // Project not found by slug or id
                _logger.LogDebug("Project query '{Query}' not found with ID or slug", query);
            }
            catch (Exception)
            {
                return new SearchResult<Project>(null, SearchStatus.ApiDown);
            }

            if (projectFoundById && project is not null)
            {
                return new SearchResult<Project>(project, SearchStatus.FoundById);
            }
        }

        try
        {
            var searchResult = await _api.SearchProjectsAsync(query);

            // No search results
            if (searchResult.TotalHits <= 0)
            {
                return new SearchResult<Project>(null, SearchStatus.NoResult);
            }
            
            // Return first result
            project = await _api.GetProjectAsync(searchResult.Hits[0].ProjectId);

            return new SearchResult<Project>(project, SearchStatus.FoundBySearch);

        }
        catch (Exception)
        {
            return new SearchResult<Project>(null, SearchStatus.ApiDown);
        }
    }
}