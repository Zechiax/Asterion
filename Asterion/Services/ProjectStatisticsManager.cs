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

        var removedEntries = 0;
        
        var currentTime = DateTime.UtcNow.AddDays(-3);

        var oldVersionStatsToKeep = db.ProjectDownloads
            .AsNoTracking()
            .Where(p => p.Date <= currentTime).Select(p => new {p.VersionId, p.Id, p.Date}).ToList();
        
        _logger.LogInformation("Found {OldEntries} entries for version statistics", oldVersionStatsToKeep.Count);
        
        // We keep only the latest version in an hour
        var idsToKeep = oldVersionStatsToKeep
            .GroupBy(p => new {p.VersionId, p.Date.Year, p.Date.Month, p.Date.Day, p.Date.Hour})
            .Select(p => p.OrderByDescending(arg => arg.Date).First().Id)
            .ToList();
        
        _logger.LogInformation("Removing old version statistics, keeping {KeptEntries} entries", idsToKeep.Count);

        //await using var versionRemovalTransaction = await db.Database.BeginTransactionAsync();

        try
        {
            db.ProjectDownloads.RemoveRange(
                db.ProjectDownloads.Where(p => p.Date < currentTime && !idsToKeep.Contains(p.Id))
            );

            removedEntries += await db.SaveChangesAsync();
            //await versionRemovalTransaction.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove old version statistics");
            //await versionRemovalTransaction.RollbackAsync();
            throw;
        }

        return removedEntries;
    }
}