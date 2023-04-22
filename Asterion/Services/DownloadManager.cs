using Asterion.Database;
using Asterion.Database.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modrinth.Models;
using Version = Modrinth.Models.Version;

namespace Asterion.Services;

public class DownloadManager
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DownloadManager> _logger;
    
    public DownloadManager(IServiceProvider services)
    {
        _services = services;
        _logger = services.GetRequiredService<ILogger<DownloadManager>>();
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
}