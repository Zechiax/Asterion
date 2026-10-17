using Asterion.Database;
using Asterion.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Asterion.Services;

/// <summary>
///     Trims old TotalDownloads rows and purges old terminal-status PendingNotifications so neither
///     table grows unbounded.
/// </summary>
public class MaintenanceService : BackgroundService
{
    private const int TotalDownloadsRetentionDays = 30;
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
                await RunTotalDownloadsCleanupAsync(stoppingToken);
                await RunPendingNotificationPurgeAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Maintenance pass failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunTotalDownloadsCleanupAsync(CancellationToken ct)
    {
        _logger.LogInformation("Running statistics database cleanup");

        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var cutoff = DateTime.UtcNow.AddDays(-TotalDownloadsRetentionDays);
        var removedEntries = await StatisticsCleanup.TrimTotalDownloadsAsync(db, cutoff, ct);

        _logger.LogInformation("Finished statistics database cleanup, removed {RemovedEntries} entries",
            removedEntries);
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
