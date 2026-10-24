using System.Text.Json;
using Asterion.ComponentBuilders;
using Asterion.Database;
using Asterion.Database.Models;
using Asterion.EmbedBuilders;
using Asterion.Services.Notifications.Delivery;
using Discord;
using Discord.WebSocket;
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

    private readonly DiscordSocketClient _client;
    private readonly ILogger<NotificationDispatchService> _logger;
    private readonly IModrinthClient _modrinthClient;
    private readonly INotificationReader _reader;
    private readonly NotificationSenderRouter _router;
    private readonly IServiceProvider _services;
    private readonly INotificationSignal _signal;

    // Set once the first Ready fires (guild cache populated); resumes after that don't re-fire Ready.
    private volatile bool _readyOnce;

    public NotificationDispatchService(IServiceProvider services, INotificationReader reader,
        INotificationSignal signal, NotificationSenderRouter router, IModrinthClient modrinthClient,
        DiscordSocketClient client, ILogger<NotificationDispatchService> logger)
    {
        _services = services;
        _reader = reader;
        _signal = signal;
        _router = router;
        _modrinthClient = modrinthClient;
        _client = client;
        _logger = logger;

        _client.Ready += () =>
        {
            _readyOnce = true;
            return Task.CompletedTask;
        };
    }

    private bool IsClientReady => _readyOnce && _client.ConnectionState == ConnectionState.Connected;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (IsClientReady)
                    await ProcessDueNotificationsAsync(stoppingToken);
                else
                    _logger.LogDebug("Discord client not ready, skipping dispatch pass");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dispatch pass failed");
            }
            
            await _signal.WaitAsync(SafetyNetPollInterval, stoppingToken);
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

        var delivery = await ResolveDeliveryAsync(db, row, ct);

        if (delivery is null)
        {
            _logger.LogWarning(
                "Entry {EntryId} / follow {FollowId} no longer exists for pending notification {Id} (guild likely unsubscribed)",
                row.EntryId, row.FollowId, row.Id);
            await _reader.MarkDeadLetteredAsync(row.Id, "Entry or follow no longer exists (guild unsubscribed)", ct);
            return;
        }

        Project project;
        Version? version;
        try
        {
            project = JsonSerializer.Deserialize<Project>(row.SerializedProject)!;

            // Milestones are not about a version and have none
            version = row.Kind == NotificationKind.DownloadMilestone
                ? null
                : JsonSerializer.Deserialize<Version>(row.SerializedVersion!)!;
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

            NotificationContent content;
            ulong? channelId;
            if (row.Kind == NotificationKind.DownloadMilestone)
            {
                var embed = ModrinthEmbedBuilder.MilestoneEmbed(project, row.Milestone ?? project.Downloads, team)
                    .Build();
                var components = new ComponentBuilder()
                    .WithButton(ModrinthComponentBuilder.GetProjectLinkButton(project)).Build();

                content = new NotificationContent(PingText(delivery.MilestonePingRoleId), embed, components);
                channelId = delivery.MilestoneChannelId;
            }
            else if (row.Kind == NotificationKind.NewProject)
            {
                var embed = ModrinthEmbedBuilder.NewProjectEmbed(project, version!, team).Build();
                var components = new ComponentBuilder()
                    .WithButton(ModrinthComponentBuilder.GetProjectLinkButton(project)).Build();

                content = new NotificationContent(PingText(delivery.NewProjectPingRoleId), embed, components);
                channelId = delivery.NewProjectChannelId;
            }
            else
            {
                var embed = ModrinthEmbedBuilder.VersionUpdateEmbed(delivery.GuildSettings, project, version!, team)
                    .Build();
                var components = new ComponentBuilder()
                    .WithButton(ModrinthComponentBuilder.GetVersionUrlButton(project, version!)).Build();

                content = new NotificationContent(PingText(delivery.PingRoleId), embed, components);
                channelId = delivery.ChannelId;
            }

            DeliveryTarget target = delivery.WebhookUrl is not null
                ? new WebhookDeliveryTarget(delivery.WebhookUrl)
                : new BotDeliveryTarget(row.GuildId, channelId ?? 0);

            await _router.SendAsync(target, content, ct);
            await _reader.MarkSentAsync(row.Id, ct);
        }
        catch (Exception ex)
        {
            await HandleFailureAsync(row, ex, ct);
        }
    }

    private static string PingText(ulong? roleId)
    {
        return roleId.HasValue ? MentionUtils.MentionRole(roleId.Value) : string.Empty;
    }

    /// <summary>
    ///     Loads the entry or the follow the row belongs to and resolves where and how to deliver it.
    ///     Returns null if neither exists anymore.
    /// </summary>
    private static async Task<DeliverySettings?> ResolveDeliveryAsync(DataContext db, PendingNotification row,
        CancellationToken ct)
    {
        if (row.EntryId is { } entryId)
        {
            var entry = await db.ModrinthEntries
                .Include(e => e.Guild).ThenInclude(g => g.GuildSettings)
                .FirstOrDefaultAsync(e => e.EntryId == entryId, ct);

            if (entry is null)
                return null;

            var settings = entry.Guild.GuildSettings;
            var pingRole = entry.CustomPingRole ?? entry.Guild.PingRole;
            return new DeliverySettings(settings, entry.CustomUpdateChannel, entry.CustomUpdateChannel,
                entry.MilestoneChannel ?? settings.MilestoneChannel ?? entry.CustomUpdateChannel,
                pingRole, pingRole, settings.MilestonePingRole, entry.WebhookUrl);
        }

        if (row.FollowId is { } followId)
        {
            var follow = await db.ModrinthFollows
                .Include(f => f.Guild).ThenInclude(g => g.GuildSettings)
                .FirstOrDefaultAsync(f => f.Id == followId, ct);

            if (follow is null)
                return null;

            var settings = follow.Guild.GuildSettings;
            var pingRole = follow.CustomPingRole ?? follow.Guild.PingRole;
            var newProjectPingRole = follow.NewProjectPingRole ?? pingRole;
            var newProjectChannel = follow.NewProjectChannel ?? follow.CustomUpdateChannel;

            // Follows have no webhook delivery (yet)
            return new DeliverySettings(settings, follow.CustomUpdateChannel, newProjectChannel,
                follow.MilestoneChannel ?? settings.MilestoneChannel ?? newProjectChannel,
                pingRole, newProjectPingRole, settings.MilestonePingRole, null);
        }

        return null;
    }

    /// <summary>Delivery settings resolved from either an entry or a follow.</summary>
    /// <param name="MilestoneChannelId">
    ///     The entry's or follow's override, then the server's milestone channel, then its announcement channel
    /// </param>
    private sealed record DeliverySettings(
        GuildSettings GuildSettings,
        ulong? ChannelId,
        ulong? NewProjectChannelId,
        ulong? MilestoneChannelId,
        ulong? PingRoleId,
        ulong? NewProjectPingRoleId,
        ulong? MilestonePingRoleId,
        string? WebhookUrl);

    private async Task HandleFailureAsync(PendingNotification row, Exception ex, CancellationToken ct)
    {
        _logger.LogError(ex,
            "Failed to dispatch pending notification {Id} to guild {GuildId} entry {EntryId} follow {FollowId} (attempt {Attempt})",
            row.Id, row.GuildId, row.EntryId, row.FollowId, row.AttemptCount + 1);

        if (ex is RateLimitedException rateLimited)
        {
            await _reader.MarkFailedAsync(row.Id, ex.Message, rateLimited.RetryAfter, ct);
            return;
        }
        
        await _reader.MarkFailedAsync(row.Id, ex.Message, NotificationReader.BackoffFor(row.AttemptCount), ct);
    }
}
