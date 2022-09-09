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
        
        
        var checkTimer = new Timer(MinutesToMilliseconds(20));
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
        try
        {
            _logger.LogInformation("Running update check");
        
            var databaseProjects = await _dataService.GetAllModrinthProjectsAsync();
            
            var projectIds = databaseProjects.Select(x => x.ProjectId);

            _logger.LogDebug("Getting multiple projects ({Count}) from Modrinth", databaseProjects.Count);
            var apiProjects = await GetMultipleProjects(projectIds);

            if (apiProjects is null)
            {
                _logger.LogWarning("Could not get information from API, update search interrupted");
                return;
            }
            
            _logger.LogDebug("Got {Count} projects", apiProjects.Length);

            var versions = apiProjects.SelectMany(p => p.Versions).ToArray();

            _logger.LogDebug("Getting multiple versions ({Count}) from Modrinth", versions.Length);

            var apiVersions = await GetMultipleVersionsAsync(versions);

            if (apiVersions is null)
            {
                _logger.LogWarning("Could not get information from API, update search interrupted");
                return;
            }
            
            _logger.LogDebug("Got {Count} versions", apiVersions.Length);

            foreach (var project in apiProjects)
            {
                _logger.LogInformation("Checking new versions for project {Title} ID {ProjectId}",project.Title ,project.Id);
                var versionList = apiVersions.Where(x => x.ProjectId == project.Id);

                var newVersions = await GetNewVersions(versionList, project.Id);

                if (newVersions is null)
                {
                    _logger.LogWarning("There was an error while finding new versions for project ID {ID}, skipping...", project.Id);
                    continue;
                }

                if (newVersions.Length == 0)
                {
                    _logger.LogInformation("No new versions for project {Title} ID {ID}",project.Title ,project.Id);
                    continue;
                }
                
                _logger.LogInformation("Found {Count} new versions", newVersions.Length);
                
                // Update data in database
                _logger.LogInformation("Updating data in database");
                await _dataService.UpdateModrinthProjectAsync(project.Id, newVersions[0].Id);

                var team = await GetProjectsTeamMembersAsync(project.Id);
                
                var guilds = await _dataService.GetAllGuildsSubscribedToProject(project.Id);

                await CheckGuilds(newVersions, project, guilds, team);
            }

            _logger.LogInformation("Update check ended");
        }
        catch (Exception exception)
        {
            _logger.LogCritical("Exception while checking for updates: {Exception} \n\nStackTrace: {StackTrace}", exception.Message, exception.StackTrace);
        }
    }

    private async Task<Version[]?> GetNewVersions(IEnumerable<Version> versionList, string projectId)
    {
        var dbProject = await _dataService.GetModrinthProjectByIdAsync(projectId);

        if (dbProject is null)
        {
            _logger.LogWarning("Project ID {ID} not found in database", projectId);
            return null;
        }

        // Ensures the data is chronologically ordered
        var orderedVersions = versionList.OrderByDescending(x => x.DatePublished);

        // Take new versions from the latest to the one we already checked
        var newVersions = orderedVersions.TakeWhile(version => version.Id != dbProject.LastCheckVersion).ToArray();

        return newVersions;
    }

    /// <summary>
    /// Will load guild's channel from custom field of entries in database and send updates
    /// </summary>
    /// <param name="versions"></param>
    /// <param name="project"></param>
    /// <param name="guilds"></param>
    /// <param name="teamMembers"></param>
    private async Task CheckGuilds(Version[] versions, Project project, IEnumerable<Guild> guilds, TeamMember[]? teamMembers = null)
    {
        foreach (var guild in guilds)
        {
            var entry = await _dataService.GetModrinthEntryAsync(guild.GuildId, project.Id);

            // Channel is not set, skip sending updates to this guild
            if (entry!.CustomUpdateChannel is null)
            {
                _logger.LogInformation("Guild ID {GuildID} has not yet set default update channel or custom channel for this project", guild.GuildId);
                continue;
            }

            var channel = _client.GetGuild(guild.GuildId).GetTextChannel((ulong)entry.CustomUpdateChannel);
                    
            _logger.LogInformation("Sending updates to guild ID {Id} and channel ID {Channel}", guild.GuildId, channel.Id);

            // None of these can be null, everything is checked beforehand
            await SendUpdatesToChannel(channel, project, versions, teamMembers);
        }
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