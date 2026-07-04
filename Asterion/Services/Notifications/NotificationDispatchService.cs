using System.Text.Json;
using Asterion.ComponentBuilders;
using Asterion.Database;
using Asterion.Database.Models;
using Asterion.EmbedBuilders;
using Asterion.Services.Notifications.Delivery;
using Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modrinth;
using Modrinth.Models;
using Version = Modrinth.Models.Version;

namespace Asterion.Services.Notifications;

/// <summary>
///     Wakes on either its own 30s safety-net poll or an immediate signal from UpdateDetectionService.
///     Dispatches sending of notification and handles errors.
/// </summary>
public class NotificationDispatchService : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan SafetyNetPollInterval = TimeSpan.FromSeconds(30);

    private readonly ILogger<NotificationDispatchService> _logger;
    private readonly IModrinthClient _modrinthClient;
    private readonly INotificationReader _reader;
    private readonly NotificationSenderRouter _router;
    private readonly IServiceProvider _services;
    private readonly INotificationSignal _signal;

    public NotificationDispatchService(IServiceProvider services, INotificationReader reader,
        INotificationSignal signal, NotificationSenderRouter router, IModrinthClient modrinthClient,
        ILogger<NotificationDispatchService> logger)
    {
        _services = services;
        _reader = reader;
        _signal = signal;
        _router = router;
        _modrinthClient = modrinthClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SafetyNetPollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueNotificationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dispatch pass failed");
            }

            var timerTick = timer.WaitForNextTickAsync(stoppingToken).AsTask();
            var signalTick = _signal.WaitAsync(stoppingToken);
            await Task.WhenAny(timerTick, signalTick);
        }
    }

    /// <summary>Runs one dispatch pass immediately - the loop's normal trigger, and also a test seam.</summary>
    public async Task ProcessDueNotificationsAsync(CancellationToken ct = default)
    {
        var due = await _reader.GetDueNotificationsAsync(BatchSize, DateTime.UtcNow, ct);
        if (due.Count == 0)
            return;

        _logger.LogInformation("Dispatching {Count} due notification(s)", due.Count);

        // Multiple guilds subscribed to the same project share the same team - avoid refetching per row.
        var teamCache = new Dictionary<string, IEnumerable<TeamMember>>();

        foreach (var row in due)
        {
            ct.ThrowIfCancellationRequested();
            await ProcessOneAsync(row, teamCache, ct);
        }
    }

    private async Task ProcessOneAsync(PendingNotification row, Dictionary<string, IEnumerable<TeamMember>> teamCache,
        CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var entry = await db.ModrinthEntries
            .Include(e => e.Guild).ThenInclude(g => g.GuildSettings)
            .FirstOrDefaultAsync(e => e.EntryId == row.EntryId, ct);

        if (entry is null)
        {
            _logger.LogWarning(
                "Entry {EntryId} no longer exists for pending notification {Id} (guild likely unsubscribed)",
                row.EntryId, row.Id);
            await _reader.MarkDeadLetteredAsync(row.Id, "Entry no longer exists (guild unsubscribed)", ct);
            return;
        }

        Project project;
        Version version;
        try
        {
            project = JsonSerializer.Deserialize<Project>(row.SerializedProject)!;
            version = JsonSerializer.Deserialize<Version>(row.SerializedVersion)!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize snapshot for pending notification {Id}", row.Id);
            await _reader.MarkDeadLetteredAsync(row.Id, $"Snapshot deserialization failed: {ex.Message}", ct);
            return;
        }

        try
        {
            if (!teamCache.TryGetValue(project.Team, out var team))
            {
                team = await _modrinthClient.Team.GetAsync(project.Team, ct);
                teamCache[project.Team] = team;
            }

            var embed = ModrinthEmbedBuilder.VersionUpdateEmbed(entry.Guild.GuildSettings, project, version, team)
                .Build();
            var components = new ComponentBuilder()
                .WithButton(ModrinthComponentBuilder.GetVersionUrlButton(project, version)).Build();

            var pingRoleId = entry.CustomPingRole ?? entry.Guild.PingRole;
            var pingText = pingRoleId.HasValue ? MentionUtils.MentionRole(pingRoleId.Value) : string.Empty;

            var content = new NotificationContent(pingText, embed, components);

            DeliveryTarget target = entry.WebhookUrl is not null
                ? new WebhookDeliveryTarget(entry.WebhookUrl)
                : new BotDeliveryTarget(row.GuildId, entry.CustomUpdateChannel ?? 0);

            await _router.SendAsync(target, content, ct);
            await _reader.MarkSentAsync(row.Id, ct);
        }
        catch (Exception ex)
        {
            await HandleFailureAsync(row, ex, ct);
        }
    }

    private async Task HandleFailureAsync(PendingNotification row, Exception ex, CancellationToken ct)
    {
        _logger.LogError(ex,
            "Failed to dispatch pending notification {Id} to guild {GuildId} entry {EntryId} (attempt {Attempt})",
            row.Id, row.GuildId, row.EntryId, row.AttemptCount + 1);

        if (ex is RateLimitedException rateLimited)
        {
            await _reader.MarkFailedAsync(row.Id, ex.Message, rateLimited.RetryAfter, ct);
            return;
        }
        
        await _reader.MarkFailedAsync(row.Id, ex.Message, NotificationReader.BackoffFor(row.AttemptCount), ct);
    }
}
