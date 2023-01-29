using System.ComponentModel;
using System.Net;
using System.Timers;
using Asterion.ComponentBuilders;
using Asterion.Database.Models;
using Asterion.EmbedBuilders;
using Asterion.Interfaces;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modrinth;
using Modrinth.Models;
using Asterion.Extensions;
using Flurl.Http;
using Version = Modrinth.Models.Version;
using Timer = System.Timers.Timer;

namespace Asterion.Services.Modrinth;

public partial class ModrinthService
{
    private readonly BackgroundWorker _updateWorker;
    private readonly IModrinthClient _api;
    private readonly ILogger _logger;
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _cacheEntryOptions;
    private readonly IDataService _dataService;
    private readonly DiscordSocketClient _client;
    private readonly IHttpClientFactory _httpClientFactory;

    public ModrinthService(IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _api = new ModrinthClient(userAgent: "Zechiax/Asterion");
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

            
            const int splitBy = 500;
            _logger.LogDebug("Getting multiple versions ({Count}) from Modrinth", versions.Length);

            // Make multiple requests to get all versions - we don't want to get 1500+ versions in one request
            // We make sure to split the requests into chunks of 500 versions
            var apiVersions = new List<Version>();
            // Split the array into chunks of 500, we use ArraySegment
            var versionChunks = System.Array.Empty<ArraySegment<string>>();
            
            for (var i = 0; i < versions.Length; i += splitBy)
            {
                _logger.LogDebug("Appending versions {Start} to {End}", i, Math.Min(splitBy, versions.Length - i));
                versionChunks = versionChunks.Append(new ArraySegment<string>(versions, i, Math.Min(splitBy, versions.Length - i))).ToArray();
            }
            
            foreach (var chunk in versionChunks)
            {
                _logger.LogDebug("Getting versions {Start} to {End}", chunk.Offset, chunk.Offset + chunk.Count);
                var versionsChunk = await GetMultipleVersionsAsync(chunk);
                if (versionsChunk is null)
                {
                    _logger.LogWarning("Could not get information from API, update search interrupted");
                    return;
                }
                apiVersions.AddRange(versionsChunk);
            }
            
            _logger.LogDebug("Got {Count} versions", apiVersions.Count);

            foreach (var project in apiProjects)
            {
                _logger.LogDebug("Checking new versions for project {Title} ID {ProjectId}",project.Title ,project.Id);
                var versionList = apiVersions.Where(x => x.ProjectId == project.Id);

                var newVersions = await GetNewVersions(versionList, project.Id);

                if (newVersions is null)
                {
                    _logger.LogWarning("There was an error while finding new versions for project ID {ID}, skipping...", project.Id);
                    continue;
                }

                if (newVersions.Length == 0)
                {
                    _logger.LogDebug("No new versions for project {Title} ID {ID}",project.Title ,project.Id);
                    continue;
                }
                
                _logger.LogInformation("Found {Count} new versions for project {Title} ID {ID}", newVersions.Length, project.Title, project.Id);
                
                // Update data in database
                _logger.LogDebug("Updating data in database");
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
            await SendUpdatesToChannel(channel, project, versions, teamMembers, guild.GuildSettings.MessageStyle);
        }
    }

    /// <summary>
    /// Sends update information about every new version to specified Text Channel
    /// </summary>
    /// <param name="textChannel"></param>
    /// <param name="currentProject"></param>
    /// <param name="newVersions"></param>
    /// <param name="team"></param>
    /// <param name="messageStyle"></param>
    private async Task SendUpdatesToChannel(SocketTextChannel textChannel, Project currentProject, IEnumerable<Version> newVersions, TeamMember[]? team, MessageStyle messageStyle = MessageStyle.Full)
    {
        // Iterate versions - they are ordered from latest to oldest, we want to sent them chronologically
        foreach (var version in newVersions.Reverse())
        {
            var embed = ModrinthEmbedBuilder.VersionUpdateEmbed(messageStyle, currentProject, version, team);
            var buttons =
                new ComponentBuilder().WithButton(
                    ModrinthComponentBuilder.GetVersionUrlButton(currentProject, version));
            try
            {
                var pingRoleId = await _dataService.GetPingRoleIdAsync(textChannel.Guild.Id);

                SocketRole? pingRole = null;
                if (pingRoleId is not null)
                {
                    pingRole = textChannel.Guild.GetRole((ulong)pingRoleId);
                }

                await textChannel.SendMessageAsync(text: pingRole?.Mention, embed: embed.Build(), components: buttons.Build());
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
    public async Task<SearchResult<ProjectDto>> FindProject(string query)
    {
        if (_cache.TryGetValue($"project-query:{query}", out var value) && value is SearchResult<ProjectDto> projectDto)
        {
            _logger.LogDebug("Project query '{Query}' in cache", query);
            return projectDto;
        }
        
        _logger.LogDebug("Project query '{Query}' not in cache", query);
        Project? project = null;
        using var httpClient = _httpClientFactory.CreateClient();

        SearchResponse? searchResponse = null;
        
        // Slug or ID can't contain space
        if (!query.Contains(' '))
        {
            var projectFoundById = false;
            try
            {
                project = await _api.Project.GetAsync(query);

                searchResponse = await _api.Project.SearchAsync(query);
                // Won't be set if exception is thrown
                projectFoundById = true;
            }
            // Not found status code is returned when requested project was not found
            catch (FlurlHttpException e) when (e.Call.Response.ResponseMessage.StatusCode == HttpStatusCode.NotFound)
            {
                // Project not found by slug or id
                _logger.LogDebug("Project query '{Query}' not found with ID or slug", query);
            }
            catch (Exception)
            {
                return new SearchResult<ProjectDto>(new ProjectDto(), SearchStatus.ApiDown);
            }

            if (projectFoundById && project is not null)
            {
                var result = new SearchResult<ProjectDto>(new ProjectDto()
                {
                    Project = project,
                    MajorColor = (await httpClient.GetMajorColorFromImageUrl(project.IconUrl)).ToDiscordColor(),
                    SearchResponse = searchResponse
                }, SearchStatus.FoundById);
                
                SetSearchResultToCache(result, query);

                return result;
            }
        }

        try
        {
            searchResponse = await _api.Project.SearchAsync(query);

            // No search results
            if (searchResponse.TotalHits <= 0)
            {
                return new SearchResult<ProjectDto>(new ProjectDto(), SearchStatus.NoResult);
            }
            
            // Return first result
            project = await _api.Project.GetAsync(searchResponse.Hits[0].ProjectId);

            var result = new SearchResult<ProjectDto>(new ProjectDto
            {
                Project = project,
                MajorColor = (await httpClient.GetMajorColorFromImageUrl(project.IconUrl)).ToDiscordColor(),
                SearchResponse = searchResponse
            }, SearchStatus.FoundBySearch);

            SetSearchResultToCache(result, query);
            
            return result;
        }
        catch (Exception e)
        {
            _logger.LogWarning("Could not get project information for query '{Query}', exception: {Message}", query, e.Message);
            return new SearchResult<ProjectDto>(new ProjectDto(), SearchStatus.ApiDown);
        }
    }

    private void SetSearchResultToCache(SearchResult<ProjectDto> searchResult, string query)
    {
        _cache.Set($"project-query:{searchResult.Payload.Project.Slug}", searchResult, absoluteExpirationRelativeToNow: TimeSpan.FromMinutes(60));
        _cache.Set($"project-query:{searchResult.Payload.Project.Id}", searchResult, absoluteExpirationRelativeToNow: TimeSpan.FromMinutes(60));
        _cache.Set($"project-query:{query}", searchResult, absoluteExpirationRelativeToNow: TimeSpan.FromMinutes(60));
    }

    public async Task<SearchResult<UserDto>> FindUser(string query)
    {
        if (_cache.TryGetValue($"user-query:{query}", out var value) && value is SearchResult<UserDto> userDto)
        {
            _logger.LogDebug("User query '{Query}' in cache", query);
            return userDto;
        }

        _logger.LogDebug("User query '{Query}' not in cache", query);
        User user;
        using var httpClient = _httpClientFactory.CreateClient();

        try
        {
            user = await _api.User.GetAsync(query);
            _logger.LogDebug("User query '{Query}' found", query);
        }
        catch (FlurlHttpException e) when (e.Call.Response.ResponseMessage.StatusCode == HttpStatusCode.NotFound)
        {
            // Project not found by slug or id
            _logger.LogDebug("User not found '{Query}'", query);
            return new SearchResult<UserDto>(new UserDto(), SearchStatus.NoResult);
        }
        catch (Exception)
        {
            return new SearchResult<UserDto>(new UserDto(), SearchStatus.ApiDown);
        }

        // User can't be null from here
        try
        {
            var projects = await _api.User.GetProjectsAsync(user.Id);
            
            var searchResult = new SearchResult<UserDto>(new UserDto
            {
                User = user,
                Projects = projects,
                MajorColor = (await httpClient.GetMajorColorFromImageUrl(user.AvatarUrl)).ToDiscordColor()
            }, SearchStatus.FoundBySearch);

            _cache.Set($"user-query:{user.Id}", searchResult, absoluteExpirationRelativeToNow: TimeSpan.FromMinutes(60));
            _cache.Set($"user-query:{query}", searchResult, absoluteExpirationRelativeToNow: TimeSpan.FromMinutes(60));
            
            return searchResult;
        }
        catch (Exception)
        {
            return new SearchResult<UserDto>(new UserDto(), SearchStatus.ApiDown);
        }

    }
}