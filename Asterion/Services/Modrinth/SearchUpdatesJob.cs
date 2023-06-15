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

        var updatedProjects = FilterUpdatedProjects(projectsDto, projects).ToArray();
        var versionsIds = updatedProjects.SelectMany(p => p.Versions).ToArray();
        
        var versions = await GetAllVersionsAsync(versionsIds);

        Dictionary<Project, IList<Version>> projectVersions = new();
        
        foreach (var project in updatedProjects)
        {
            var projectVersionsList = versions.Where(v => v.ProjectId == project.Id)
                .OrderByDescending(v => v.DatePublished)
                .ToList();
            projectVersions.Add(project, projectVersionsList);
        }
        
        // This will modify the projectVersions dictionary in-place and only keep the projects and versions that have updates
        await CheckForUpdatesAsync(projectVersions);
        
        // TODO: Create DiscordNotifyJob

        // Let's await all the background tasks we started
        await updateStatsTask;
    }
    
    private async Task CheckForUpdatesAsync(Dictionary<Project, IList<Version>> projectVersions)
    {
        // This list will hold the keys to remove from the projectVersions dictionary
        var keysToRemove = new List<Project>();

        // Loop through the projects in the dictionary
        foreach (var (project, versions) in projectVersions)
        {
            var latestVersion = versions.First();
            var dbProject = await _dataService.GetModrinthProjectByIdAsync(project.Id);
            if (dbProject == null)
            {
                _logger.LogError("Failed to find project {ProjectId} in the database", project.Id);
                keysToRemove.Add(project);
                continue;
            }

            if (dbProject.LastCheckVersion == latestVersion.Id)
            {
                keysToRemove.Add(project);
                continue;
            }

            _logger.LogInformation("Found update for project {ProjectId}", project.Id);
            var success = await _dataService.UpdateModrinthProjectAsync(dbProject.ProjectId, latestVersion.Id, project.Title,
                DateTime.UtcNow);

            if (!success)
            {
                _logger.LogError("Failed to update project {ProjectId} in the database", project.Id);
                keysToRemove.Add(project);
                continue;
            }
    
            // Remove versions from the list that are the same or newer than the latest version
            // This modifies the list associated with the project in the dictionary
            var keepVersions = versions.Where(v => v.DatePublished >= latestVersion.DatePublished).ToList();
            projectVersions[project] = keepVersions;
        }

        // Now remove the projects from the dictionary that were added to the keysToRemove list
        // This modifies the dictionary in-place
        foreach (var key in keysToRemove)
        {
            projectVersions.Remove(key);
        }
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