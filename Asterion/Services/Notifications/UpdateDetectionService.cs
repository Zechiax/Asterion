using Asterion.Database;
using Asterion.Database.Models;
using Asterion.Extensions;
using Asterion.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modrinth;
using Modrinth.Models;
using Version = Modrinth.Models.Version;

namespace Asterion.Services.Notifications;

/// <summary>
///     Polls Modrinth for project updates and enqueues PendingNotification rows.
/// </summary>
public class UpdateDetectionService : BackgroundService
{
    private const int SplitSize = 100;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(10);

    private readonly IModrinthClient _client;
    private readonly IDataService _dataService;
    private readonly ILogger<UpdateDetectionService> _logger;
    private readonly INotificationScheduler _notificationScheduler;
    private readonly INotificationSignal _notificationSignal;
    private readonly ProjectStatisticsManager _projectStatisticsManager;
    private readonly IServiceProvider _services;

    public UpdateDetectionService(IServiceProvider services, IModrinthClient client, IDataService dataService,
        ProjectStatisticsManager projectStatisticsManager, INotificationScheduler notificationScheduler,
        INotificationSignal notificationSignal, ILogger<UpdateDetectionService> logger)
    {
        _services = services;
        _client = client;
        _dataService = dataService;
        _projectStatisticsManager = projectStatisticsManager;
        _notificationScheduler = notificationScheduler;
        _notificationSignal = notificationSignal;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        do
        {
            try
            {
                await RunDetectionPassAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update detection pass failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Runs a detection pass immediately, bypassing the poll interval.</summary>
    public Task TriggerImmediateCheckAsync(CancellationToken ct = default)
    {
        return RunDetectionPassAsync(ct);
    }

    private async Task RunDetectionPassAsync(CancellationToken ct)
    {
        _logger.LogInformation("Update detection pass started");

        var projectsDto = await _dataService.GetAllModrinthProjectsAsync();
        _logger.LogDebug("Found {Count} projects", projectsDto.Count);

        var projects = await _client.Project.GetMultipleAsync(projectsDto.Select(p => p.ProjectId), ct);
        var projectList = projects.ToList();

        await UpdateStatisticsDataAsync(projectList);

        var updatedProjects = FilterUpdatedProjects(projectsDto, projectList).ToArray();

        if (updatedProjects.Length == 0)
        {
            _logger.LogInformation("No projects with updates found");
            return;
        }

        _logger.LogInformation("Found {Count} projects with updates", updatedProjects.Length);

        var versionIds = updatedProjects.SelectMany(p => p.Versions).ToArray();
        var versionsList = await GetAllVersionsAsync(versionIds);

        foreach (var project in updatedProjects)
        {
            ct.ThrowIfCancellationRequested();

            var projectVersions = versionsList
                .Where(v => v.ProjectId == project.Id)
                .OrderByDescending(v => v.DatePublished)
                .ToList();

            if (projectVersions.Count == 0)
                continue;

            await ProcessProjectUpdateAsync(project, projectVersions, ct);
        }
    }

    private async Task ProcessProjectUpdateAsync(Project project, IList<Version> versions, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var dbProject = await db.ModrinthProjects.FindAsync([project.Id], ct);
        if (dbProject is null)
        {
            _logger.LogError("Failed to find project {ProjectId} in the database", project.Id);
            return;
        }

        var latestVersion = versions.First();
        if (dbProject.LastCheckVersion == latestVersion.Id)
            return;

        _logger.LogInformation("Found update for project {ProjectId}", project.Id);

        var keepVersions = versions
            .Where(v => v.DatePublished <= latestVersion.DatePublished && v.DatePublished > dbProject.LastUpdated)
            .OrderBy(v => v.DatePublished)
            .ToList();

        var entries = await db.ModrinthEntries.Where(e => e.ProjectId == project.Id).ToListAsync(ct);

        foreach (var version in keepVersions)
            _notificationScheduler.EnqueueForEntries(db, project, version, entries);

        dbProject.LastUpdated = DateTime.UtcNow;
        dbProject.LastCheckVersion = latestVersion.Id;
        dbProject.Title = project.Title;

        // Single atomic commit: project's check-state and every PendingNotification row land together.
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Enqueued notifications for {VersionCount} new version(s) of project {ProjectId}",
            keepVersions.Count, project.Id);

        _notificationSignal.Signal();
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

    private async Task UpdateStatisticsDataAsync(IEnumerable<Project> projects)
    {
        var projectList = projects.ToList();
        _logger.LogDebug("Updating statistics for {Count} projects", projectList.Count);

        foreach (var project in projectList)
            await _projectStatisticsManager.UpdateDownloadsAsync(project);

        _logger.LogDebug("Statistics update finished");
    }

    private static IEnumerable<Project> FilterUpdatedProjects(IList<ModrinthProject> projectsDto,
        IEnumerable<Project> projects)
    {
        var updatedProjects = new List<Project>();

        foreach (var project in projects)
        {
            var projectDto = projectsDto.First(p => p.ProjectId == project.Id);
            if (projectDto.LastUpdated < project.Updated)
                updatedProjects.Add(project);
        }

        return updatedProjects;
    }
}
