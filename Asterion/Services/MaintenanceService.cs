using Asterion.Database;
using Asterion.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Asterion.Services;

/// <summary>
///     Deletes old hourly statistics and purges old terminal-status PendingNotifications so neither
///     table grows unbounded. Daily statistics are kept forever. Also removes projects nothing uses anymore.
/// </summary>
public class MaintenanceService : BackgroundService
{
    private const int HourlyStatsRetentionDays = 30;
    private const int PendingNotificationRetentionDays = 14;
    private static readonly TimeSpan RunInterval = TimeSpan.FromDays(1);

    private readonly ILogger<MaintenanceService> _logger;
    private readonly IServiceProvider _services;

    public MaintenanceService(IServiceProvider services, ILogger<MaintenanceService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RunInterval);
        
        do
        {
            try
            {
                await RunHourlyStatsCleanupAsync(stoppingToken);
                await RunPendingNotificationPurgeAsync(stoppingToken);
                await RemoveUnusedProjectsAsync(stoppingToken);
                await ReclaimFreePagesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Maintenance pass failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunHourlyStatsCleanupAsync(CancellationToken ct)
    {
        _logger.LogInformation("Running statistics database cleanup");

        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var cutoff = DateTime.UtcNow.AddDays(-HourlyStatsRetentionDays);
        var removedEntries = await db.HourlyProjectStats
            .Where(s => s.Hour < cutoff)
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation("Finished statistics database cleanup, removed {RemovedEntries} entries",
            removedEntries);
    }

    private async Task RemoveUnusedProjectsAsync(CancellationToken ct)
    {
        _logger.LogInformation("Removing projects with no entries or follows");

        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var removed = await ProjectCleanup.RemoveAllUnusedProjectsAsync(db, ct);

        _logger.LogInformation("Removed {Count} unused projects", removed);
    }

    /// <summary>
    ///     Gives the pages freed by the deletes back to the file system, the database runs with
    ///     auto_vacuum = INCREMENTAL so it doesn't happen on its own
    /// </summary>
    private async Task ReclaimFreePagesAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        if (db.Database.IsSqlite())
            await db.Database.ExecuteSqlRawAsync("PRAGMA incremental_vacuum;", ct);
    }

    private async Task RunPendingNotificationPurgeAsync(CancellationToken ct)
    {
        _logger.LogInformation("Running pending notification purge");

        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var cutoff = DateTime.UtcNow.AddDays(-PendingNotificationRetentionDays);

        var removedEntries = await db.PendingNotifications
            .Where(p => (p.Status == NotificationStatus.Sent || p.Status == NotificationStatus.DeadLettered)
                        && (p.LastAttemptAt ?? p.CreatedAt) < cutoff)
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation("Finished pending notification purge, removed {RemovedEntries} entries",
            removedEntries);
    }
}
