using Asterion.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Asterion.Services.Follows;

public class FollowSyncService : BackgroundService
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(15);

    private readonly IFollowService _followService;
    private readonly ILogger<FollowSyncService> _logger;

    public FollowSyncService(IFollowService followService, ILogger<FollowSyncService> logger)
    {
        _followService = followService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SyncInterval);

        do
        {
            try
            {
                await RunSyncPassAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Follow sync pass failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Runs one sync pass immediately - the loop's normal trigger, and also a test seam.</summary>
    public async Task RunSyncPassAsync(CancellationToken ct = default)
    {
        var follows = await _followService.GetAllFollowsAsync(ct);
        if (follows.Count == 0)
            return;

        _logger.LogInformation("Follow sync pass started for {Count} follow(s)", follows.Count);

        // Follows of the same target share one source call
        foreach (var group in follows.GroupBy(f => (f.TargetType, f.TargetId)))
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var source = await _followService.FetchTargetAsync(group.Key.TargetType, group.Key.TargetId, ct);

                foreach (var follow in group)
                    await _followService.SyncFollowAsync(follow.Id, source, announceNew: true, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Follow sync failed for {TargetType} {TargetId}", group.Key.TargetType,
                    group.Key.TargetId);
            }
        }

        _logger.LogInformation("Follow sync pass finished");
    }
}
