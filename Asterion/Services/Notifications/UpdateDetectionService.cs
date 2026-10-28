using Asterion.Configuration;
using Asterion.Database;
using Asterion.Database.Models;
using Asterion.Extensions;
using Asterion.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

    private readonly TimeSpan _checkInterval;
    private readonly IModrinthClient _client;
    private readonly IDataService _dataService;
    private readonly ILogger<UpdateDetectionService> _logger;
    private readonly INotificationScheduler _notificationScheduler;
    private readonly INotificationSignal _notificationSignal;
    private readonly ProjectStatisticsManager _projectStatisticsManager;
    private readonly IServiceProvider _services;

    public UpdateDetectionService(IServiceProvider services, IModrinthClient client, IDataService dataService,
        ProjectStatisticsManager projectStatisticsManager, INotificationScheduler notificationScheduler,
        INotificationSignal notificationSignal, IConfiguration config, ILogger<UpdateDetectionService> logger)
    {
        _checkInterval = TimeSpan.FromMinutes(config.GetValue(ConfigKeys.UpdateCheckIntervalMinutes,
            ConfigKeys.DefaultUpdateCheckIntervalMinutes));
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
        using var timer = new PeriodicTimer(_checkInterval);

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

        await UpdateStatisticsDataAsync(projectList, ct);

        // A failed milestone check must not stop the update messages
        try
        {
            await ProcessMilestonesAsync(projectsDto, projectList, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Download milestone check failed");
        }

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
        var guildIdsWithEntry = entries.Select(e => e.GuildId).ToHashSet();

        var follows = await GetFollowTargetsAsync(db, project.Id, ct);

        foreach (var version in keepVersions)
        {
            _notificationScheduler.EnqueueForEntries(db, project, version, entries);
            _notificationScheduler.EnqueueForFollows(db, project, version, follows, guildIdsWithEntry);
        }

        dbProject.LastUpdated = DateTime.UtcNow;
        dbProject.LastCheckVersion = latestVersion.Id;
        dbProject.Title = project.Title;

        // Single atomic commit: project's check-state and every PendingNotification row land together.
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Enqueued notifications for {VersionCount} new version(s) of project {ProjectId}",
            keepVersions.Count, project.Id);

        _notificationSignal.Signal();
    }

    /// <summary>
    ///     Enqueues milestone messages for projects whose downloads passed a milestone since the last check.
    ///     The first check of a project only records its downloads, so nothing is sent for milestones reached
    ///     before the bot tracked it.
    /// </summary>
    /// <param name="projectsDto">The projects read at the start of the pass, holding the last checked downloads</param>
    private async Task ProcessMilestonesAsync(IEnumerable<ModrinthProject> projectsDto,
        IReadOnlyCollection<Project> projects, CancellationToken ct)
    {
        var checkedDownloads = projectsDto.ToDictionary(p => p.ProjectId, p => p.MilestoneCheckedDownloads);

        // Only projects never checked or with more downloads are loaded and saved. Ignores decreases, so a
        // milestone is not sent again after Modrinth removes downloads.
        var changed = projects
            .Where(p => checkedDownloads.TryGetValue(p.Id, out var previous) &&
                        (previous is null || p.Downloads > previous))
            .ToList();

        if (changed.Count == 0)
            return;

        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var changedIds = changed.Select(p => p.Id).ToList();
        var dbProjects = await db.ModrinthProjects
            .Where(p => changedIds.Contains(p.ProjectId))
            .ToDictionaryAsync(p => p.ProjectId, ct);
        var enqueued = 0;

        foreach (var project in changed)
        {
            // Removed since the pass started
            if (!dbProjects.TryGetValue(project.Id, out var dbProject))
                continue;

            var previous = checkedDownloads[project.Id];
            var current = (long)project.Downloads;
            dbProject.MilestoneCheckedDownloads = current;

            // The standard ladder has every milestone of the other ladders, no guild can have one if it has none
            if (previous is null ||
                DownloadMilestones.HighestCrossed(previous.Value, current, MilestoneLadder.Standard) is null)
                continue;

            var entries = await db.ModrinthEntries
                .Include(e => e.Guild).ThenInclude(g => g.GuildSettings)
                .Where(e => e.ProjectId == project.Id)
                .ToListAsync(ct);

            var follows = await db.FollowProjects
                .Where(fp => fp.ProjectId == project.Id && !fp.Ignored)
                .Select(fp => fp.Follow)
                .Where(f => (f.Events & FollowEvents.DownloadMilestone) != 0)
                .Include(f => f.Guild).ThenInclude(g => g.GuildSettings)
                .ToListAsync(ct);

            enqueued += _notificationScheduler.EnqueueMilestone(db, project, previous.Value, current, entries,
                follows);
        }

        // Single atomic commit: the checked downloads and the milestone rows land together
        await db.SaveChangesAsync(ct);

        if (enqueued == 0)
            return;

        _logger.LogInformation("Enqueued {Count} download milestone notification(s)", enqueued);
        _notificationSignal.Signal();
    }

    /// <summary>
    ///     Returns the follows that want update messages for the project: they have the ProjectUpdate event
    ///     and a FollowProject row for the project that is not ignored.
    /// </summary>
    public static Task<List<ModrinthFollow>> GetFollowTargetsAsync(DataContext db, string projectId,
        CancellationToken ct = default)
    {
        return db.FollowProjects
            .Where(fp => fp.ProjectId == projectId && !fp.Ignored)
            .Select(fp => fp.Follow)
            .Where(f => (f.Events & FollowEvents.ProjectUpdate) != 0)
            .ToListAsync(ct);
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

    private async Task UpdateStatisticsDataAsync(IReadOnlyCollection<Project> projects, CancellationToken ct)
    {
        _logger.LogDebug("Updating statistics for {Count} projects", projects.Count);

        await _projectStatisticsManager.RecordAsync(projects, ct);

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
