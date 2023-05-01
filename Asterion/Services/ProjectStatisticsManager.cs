using System.Timers;
using Asterion.Database;
using Asterion.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modrinth.Models;
using Version = Modrinth.Models.Version;
using Timer = System.Timers.Timer;

namespace Asterion.Services;

public class ProjectStatisticsManager
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ProjectStatisticsManager> _logger;
    private readonly Timer _databaseCleanupTimer;
    
    public ProjectStatisticsManager(IServiceProvider services)
    {
        _services = services;
        _logger = services.GetRequiredService<ILogger<ProjectStatisticsManager>>();

        _databaseCleanupTimer = new Timer(TimeSpan.FromHours(24));
        _databaseCleanupTimer.Elapsed += DatabaseCleanupTimerElapsed;
        _databaseCleanupTimer.Start();
        
        DatabaseCleanupTimerElapsed(null, null);
    }

    private async void DatabaseCleanupTimerElapsed(object? state, ElapsedEventArgs? elapsedEventArgs)
    {
        _logger.LogInformation("Running statistics database cleanup");
        var removedEntries = await FreeSpaceFromUnusedEntries();
        _logger.LogInformation("Finished statistics database cleanup, removed {RemovedProjects} entries", removedEntries);
    }

    public async Task UpdateDownloadsAsync(Project project, IEnumerable<Version> version)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        
        // Get the project from the database
        var dbProject = await db.ModrinthProjects.FindAsync(project.Id);
        
        var timestamp = DateTime.UtcNow;
        
        // If the project doesn't exist, we fail
        if (dbProject == null)
        {
            _logger.LogError("Failed to update downloads for project {ProjectId} because it doesn't exist in the database", project.Id);
            return;
        }
        
        // We create new total downloads for the project
        var totalDownloads = new TotalDownloads
        {
            ProjectId = dbProject.ProjectId,
            Downloads = project.Downloads,
            Timestamp = timestamp
        };
        
        db.TotalDownloads.Add(totalDownloads);
        
        // We create new project downloads for each version
        List<ProjectDownload> projectDownloads = new();
        
        foreach (var v in version)
        {
            var projectDownload = new ProjectDownload
            {
                ProjectId = dbProject.ProjectId,
                VersionId = v.Id,
                Downloads = v.Downloads,
                Date = timestamp
            };
            
            projectDownloads.Add(projectDownload);
        }
        
        db.ProjectDownloads.AddRange(projectDownloads);
        
        await db.SaveChangesAsync();
    }
    
    public async Task<ICollection<ProjectDownload>> GetProjectDownloadsAsync(string projectId)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        
        return await db.ProjectDownloads
            .Where(p => p.ProjectId == projectId)
            .ToListAsync();
    }
    
    public async Task<ICollection<TotalDownloads>> GetTotalDownloadsAsync(string projectId)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        
        return await db.TotalDownloads
            .Where(p => p.ProjectId == projectId)
            .ToListAsync();
    }

    public async Task<int> FreeSpaceFromUnusedEntries()
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var currentTime = DateTime.UtcNow;
        
        // For every version stats, we get all that are older than 3 days
        var oldVersionStats = db.ProjectDownloads
            .Where(p => p.Date < currentTime.AddDays(-3));
        
        // We keep only 1 entry per hour, as we don't need more, the entry with the highest download count in that hour
        // is the one we want to keep, as it's the most recent one
        var oldVersionStatsToKeep = oldVersionStats
            .GroupBy(p => new { p.ProjectId, p.VersionId, p.Date.Year, p.Date.Month, p.Date.Day, p.Date.Hour })
            .Select(g => g.OrderByDescending(p => p.Downloads).First());
        
        // We delete all the entries that are older than 3 days and are not the most recent one in that hour
        db.ProjectDownloads.RemoveRange(oldVersionStats.Where(p => !oldVersionStatsToKeep.Contains(p)));

        // We do the same for total downloads
        var oldTotalDownloads = db.TotalDownloads
            .Where(p => p.Timestamp < currentTime.AddDays(-3));
        
        var oldTotalDownloadsToKeep = oldTotalDownloads
            .GroupBy(p => new { p.ProjectId, p.Timestamp.Year, p.Timestamp.Month, p.Timestamp.Day, p.Timestamp.Hour })
            .Select(g => g.OrderByDescending(p => p.Downloads).First());

        db.TotalDownloads.RemoveRange(oldTotalDownloads.Except(oldTotalDownloadsToKeep));
        
        return await db.SaveChangesAsync();
    }
}