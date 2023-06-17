using System.ComponentModel;
using System.Net;
using Asterion.Extensions;
using Asterion.Interfaces;
using Discord.WebSocket;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modrinth;
using Modrinth.Exceptions;
using Modrinth.Models;
using Quartz;

namespace Asterion.Services.Modrinth;

public partial class ModrinthService
{
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _cacheEntryOptions;
    private readonly DiscordSocketClient _client;
    private readonly IDataService _dataService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;
    private readonly ProjectStatisticsManager _projectStatisticsManager;
    private readonly BackgroundWorker _updateWorker;
    private readonly IScheduler _scheduler;
    private readonly JobKey _jobKey;
    protected IModrinthClient Api { get; }

    public ModrinthService(IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory, ISchedulerFactory scheduler)
    {
        _httpClientFactory = httpClientFactory;
        Api = serviceProvider.GetRequiredService<IModrinthClient>();
        _logger = serviceProvider.GetRequiredService<ILogger<ModrinthService>>();
        _cache = serviceProvider.GetRequiredService<IMemoryCache>();
        _dataService = serviceProvider.GetRequiredService<IDataService>();
        _client = serviceProvider.GetRequiredService<DiscordSocketClient>();
        _projectStatisticsManager = serviceProvider.GetRequiredService<ProjectStatisticsManager>();
        _scheduler = scheduler.GetScheduler().GetAwaiter().GetResult();

        _cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
        };

        var job = JobBuilder.Create<SearchUpdatesJob>()
            .WithIdentity("SearchUpdatesJob", "Modrinth")
            .Build();
        
        _jobKey = job.Key;
        
        // Every 10 minutes
        var trigger = TriggerBuilder.Create()
            .WithIdentity("SearchUpdatesTrigger", "Modrinth")
            .StartAt(DateBuilder.FutureDate(10, IntervalUnit.Minute)) // Start 10 minutes from now
            .WithSimpleSchedule(x => x
                .WithIntervalInMinutes(10)
                .RepeatForever())
            .Build();
        
        _scheduler.ScheduleJob(job, trigger);

        _logger.LogInformation("Modrinth service initialized");
    }

    /// <summary>
    ///     Force check for updates, used for debugging
    /// </summary>
    /// <returns>False if worker is busy</returns>
    public bool ForceUpdate()
    {
        _scheduler.TriggerJob(_jobKey);
        
        return true;
    }

    /// <summary>
    ///     Tries to find project on Modrinth
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
                project = await Api.Project.GetAsync(query);

                searchResponse = await Api.Project.SearchAsync(query);
                // Won't be set if exception is thrown
                projectFoundById = true;
            }
            // Not found status code is returned when requested project was not found
            catch (ModrinthApiException e) when (e.Response.StatusCode == HttpStatusCode.NotFound)
            {
                // Project not found by slug or id
                _logger.LogDebug("Project query '{Query}' not found with ID or slug", query);
            }
            catch (ModrinthApiException e)
            {
                _logger.LogDebug(e, "Error while searching for project '{Query}'", query);
                return new SearchResult<ProjectDto>(new ProjectDto(), SearchStatus.ApiDown, query);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while searching for project '{Query}'", query);
                return new SearchResult<ProjectDto>(new ProjectDto(), SearchStatus.ApiDown, query);
            }

            if (projectFoundById && project is not null)
            {
                var result = new SearchResult<ProjectDto>(new ProjectDto
                {
                    Project = project,
                    SearchResponse = searchResponse
                }, SearchStatus.FoundById, query);

                SetSearchResultToCache(result, query);

                return result;
            }
        }

        try
        {
            searchResponse = await Api.Project.SearchAsync(query);

            // No search results
            if (searchResponse.TotalHits <= 0)
                return new SearchResult<ProjectDto>(new ProjectDto(), SearchStatus.NoResult, query);

            // Return first result
            project = await Api.Project.GetAsync(searchResponse.Hits[0].ProjectId);

            var result = new SearchResult<ProjectDto>(new ProjectDto
            {
                Project = project,
                SearchResponse = searchResponse
            }, SearchStatus.FoundBySearch, query);

            SetSearchResultToCache(result, query);

            return result;
        }
        catch (Exception e)
        {
            _logger.LogWarning("Could not get project information for query '{Query}', exception: {Message}", query,
                e.Message);
            return new SearchResult<ProjectDto>(new ProjectDto(), SearchStatus.ApiDown, query);
        }
    }

    private void SetSearchResultToCache(SearchResult<ProjectDto> searchResult, string query)
    {
        _cache.Set($"project-query:{searchResult.Payload.Project.Slug}", searchResult, TimeSpan.FromMinutes(60));
        _cache.Set($"project-query:{searchResult.Payload.Project.Id}", searchResult, TimeSpan.FromMinutes(60));
        _cache.Set($"project-query:{query}", searchResult, TimeSpan.FromMinutes(60));
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
            user = await Api.User.GetAsync(query);
            _logger.LogDebug("User query '{Query}' found", query);
        }
        catch (ModrinthApiException e) when (e.Response.StatusCode == HttpStatusCode.NotFound)
        {
            // Project not found by slug or id
            _logger.LogDebug("User not found '{Query}'", query);
            return new SearchResult<UserDto>(new UserDto(), SearchStatus.NoResult, query);
        }
        catch (Exception)
        {
            return new SearchResult<UserDto>(new UserDto(), SearchStatus.ApiDown, query);
        }

        // User can't be null from here
        try
        {
            var projects = await Api.User.GetProjectsAsync(user.Id);

            var searchResult = new SearchResult<UserDto>(new UserDto
            {
                User = user,
                Projects = projects,
                MajorColor = (await httpClient.GetMajorColorFromImageUrl(user.AvatarUrl)).ToDiscordColor()
            }, SearchStatus.FoundBySearch, query);

            _cache.Set($"user-query:{user.Id}", searchResult, TimeSpan.FromMinutes(60));
            _cache.Set($"user-query:{query}", searchResult, TimeSpan.FromMinutes(60));

            return searchResult;
        }
        catch (Exception)
        {
            return new SearchResult<UserDto>(new UserDto(), SearchStatus.ApiDown, query);
        }
    }
}