using Asterion.Database;
using Asterion.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modrinth.Models;

namespace Asterion.Services;

public class ProjectStatisticsManager
{
    private readonly ILogger<ProjectStatisticsManager> _logger;
    private readonly IServiceProvider _services;

    public ProjectStatisticsManager(IServiceProvider services)
    {
        _services = services;
        _logger = services.GetRequiredService<ILogger<ProjectStatisticsManager>>();
    }

    /// <summary>
    ///     Stores the current counts of the projects into this hour's and this day's rows, overwriting
    ///     earlier samples of the same hour and day, so each row ends up with the last sample
    /// </summary>
    public async Task RecordAsync(IReadOnlyCollection<Project> projects, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var now = DateTime.UtcNow;
        var hour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
        var day = now.Date;

        var known = (await db.ModrinthProjects.Select(p => p.ProjectId).ToListAsync(ct)).ToHashSet();
        var hourly = await db.HourlyProjectStats.Where(s => s.Hour == hour).ToDictionaryAsync(s => s.ProjectId, ct);
        var daily = await db.DailyProjectStats.Where(s => s.Day == day).ToDictionaryAsync(s => s.ProjectId, ct);

        foreach (var project in projects)
        {
            // Stats rows reference the project, it may have been removed since the poll started
            if (!known.Contains(project.Id))
            {
                _logger.LogDebug("Skipping statistics of project {ProjectId}, it's not in the database", project.Id);
                continue;
            }

            if (!hourly.TryGetValue(project.Id, out var hourStats))
            {
                hourStats = new HourlyProjectStats { ProjectId = project.Id, Hour = hour };
                db.HourlyProjectStats.Add(hourStats);
            }

            hourStats.Downloads = project.Downloads;
            hourStats.Followers = project.Followers;

            if (!daily.TryGetValue(project.Id, out var dayStats))
            {
                dayStats = new DailyProjectStats { ProjectId = project.Id, Day = day };
                db.DailyProjectStats.Add(dayStats);
            }

            dayStats.Downloads = project.Downloads;
            dayStats.Followers = project.Followers;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<List<HourlyProjectStats>> GetHourlyAsync(string projectId, DateTime from)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        return await db.HourlyProjectStats.AsNoTracking()
            .Where(s => s.ProjectId == projectId && s.Hour >= from)
            .OrderBy(s => s.Hour)
            .ToListAsync();
    }

    public async Task<List<DailyProjectStats>> GetDailyAsync(string projectId, DateTime from)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        return await db.DailyProjectStats.AsNoTracking()
            .Where(s => s.ProjectId == projectId && s.Day >= from)
            .OrderBy(s => s.Day)
            .ToListAsync();
    }
}
