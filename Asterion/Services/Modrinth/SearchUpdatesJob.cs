using Asterion.Database.Models;
using Asterion.Extensions;
using Asterion.Interfaces;
using Microsoft.Extensions.Logging;
using Modrinth;
using Modrinth.Models;
using Quartz;
using Version = Modrinth.Models.Version;

namespace Asterion.Services.Modrinth;

public class SearchUpdatesJob : IJob
{
    private readonly IModrinthClient _client;
    private readonly IDataService _dataService;
    private readonly ILogger<SearchUpdatesJob> _logger;
    private readonly ProjectStatisticsManager _projectStatisticsManager;
    
    private const int SplitSize = 100;
    
    public SearchUpdatesJob(IModrinthClient client, IDataService dataService, ILogger<SearchUpdatesJob> logger, ProjectStatisticsManager projectStatisticsManager)
    {
        _client = client;
        _dataService = dataService;
        _logger = logger;
        _projectStatisticsManager = projectStatisticsManager;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Update search job started");
        var time = DateTime.UtcNow;

        try
        {
            await DoExecutionWork();
        }
        // We should only catch exception that we know could be thrown, as Quartz will reschedule the job if we throw
        finally
        {
            
        }

    }

    private async Task DoExecutionWork()
    {
        // We won't catch any exceptions here, it's not this methods responsibility to handle them
        var projectsDto = await GetProjectsAsync();
        var projectIds = projectsDto.Select(p => p.ProjectId);
        
        var projects = await _client.Project.GetMultipleAsync(projectIds);

        // We let the statistics update run in the background, as it's not critical to the job
        var copy = projects.ToList();
        var updateStatsTask = UpdateStatisticsData(copy);

        var updatedProjects = FilterUpdatedProjects(projectsDto, projects);
        var versionsIds = updatedProjects.SelectMany(p => p.Versions).ToArray();
        
        var versions = await GetAllVersionsAsync(versionsIds);
        

        // Let's await all the background tasks we started
        await updateStatsTask;
    }

    private async Task<IList<Version>> GetAllVersionsAsync(string[] versionIds)
    {
        var versionSegments = versionIds.Split(SplitSize).ToList();
        var versions = new List<Version>();
        
        foreach (var segment in versionSegments)
        {
            var segmentVersions = await _client.Version.GetMultipleAsync(segment);
            versions.AddRange(segmentVersions);
        }
        
        return versions;
    }

    private async Task UpdateStatisticsData(IEnumerable<Project> projects)
    {
        var projectList = projects.ToList();
        _logger.LogDebug("Updating statistics for {Count} projects", projectList.Count);
        foreach (var project in projectList)
        {
            await _projectStatisticsManager.UpdateDownloadsAsync(project);
        }
        _logger.LogDebug("Statistics update finished");
    }
    
    private static IEnumerable<Project> FilterUpdatedProjects(IList<ModrinthProject> projectsDto, IEnumerable<Project> projects)
    {
        var updatedProjects = new List<Project>();
        foreach (var project in projects)
        {
            var projectDto = projectsDto.First(p => p.ProjectId == project.Id);
            if (projectDto.LastUpdated < project.Updated)
            {
                updatedProjects.Add(project);
            }
        }

        return updatedProjects;
    }

    private async Task<IList<ModrinthProject>> GetProjectsAsync()
    {
        var projects = await _dataService.GetAllModrinthProjectsAsync();
        _logger.LogDebug("Found {Count} projects", projects.Count);
        return projects;
    } 
}