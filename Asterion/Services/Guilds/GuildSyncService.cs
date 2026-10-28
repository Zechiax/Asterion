using Asterion.Database;
using Asterion.Database.Models;
using Asterion.Interfaces;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Asterion.Services.Guilds;

/// <summary>
///     Keeps the stored guilds in sync with the guilds the bot is in
/// </summary>
public class GuildSyncService
{
    private readonly DiscordSocketClient _client;
    private readonly IGuildService _guildService;
    private readonly ILogger<GuildSyncService> _logger;
    private readonly IServiceProvider _services;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public GuildSyncService(IServiceProvider services, DiscordSocketClient client, IGuildService guildService,
        ILogger<GuildSyncService> logger)
    {
        _services = services;
        _client = client;
        _guildService = guildService;
        _logger = logger;
    }

    public void Initialize()
    {
        _client.JoinedGuild += guild => HandleJoinedAsync(guild.Id);
        _client.LeftGuild += guild => HandleLeftAsync(guild.Id);

        // Off the gateway task, a long sync would block it and miss heartbeats
        _client.Ready += () =>
        {
            _ = Task.Run(RunSyncAsync);
            return Task.CompletedTask;
        };
    }

    /// <summary>
    ///     Registers the guild, or activates it again if its data was kept when the bot left
    /// </summary>
    private async Task HandleJoinedAsync(ulong guildId)
    {
        if (!await _guildService.AddGuildAsync(guildId))
            await _guildService.UpdateGuildAsync(guildId, g => g.Active = true);
    }

    /// <summary>
    ///     Removes the guild, or only deactivates it if its settings say to keep its data
    /// </summary>
    private async Task HandleLeftAsync(ulong guildId)
    {
        var guild = await _guildService.GetGuildAsync(guildId);
        if (guild is null)
            return;

        if (guild.GuildSettings.RemoveOnLeave == false)
        {
            _logger.LogInformation("Keeping data of left guild ID {ID}, deactivating it", guildId);
            await _guildService.UpdateGuildAsync(guildId, g => g.Active = false);
            return;
        }

        await _guildService.RemoveGuildAsync(guildId);
    }

    private async Task RunSyncAsync()
    {
        // A reconnect fires Ready again, the previous sync may still be running
        if (!await _syncLock.WaitAsync(0))
        {
            _logger.LogInformation("Guild sync is already running, skipping");
            return;
        }

        try
        {
            await CreateMissingGuildSettingsAsync();
            await RegisterNewGuildsAsync();
            await RemoveLeftGuildsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Guild sync failed");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task CreateMissingGuildSettingsAsync()
    {
        _logger.LogDebug("Creating missing guild setting entries");

        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var guilds = await db.Guilds
            .Where(g => !db.GuildSettings.Any(s => s.GuildId == g.GuildId))
            .ToListAsync();

        if (guilds.Count == 0)
            return;

        foreach (var guild in guilds)
            guild.GuildSettings = new GuildSettings { Guild = guild };

        await db.SaveChangesAsync();
    }

    /// <summary>
    ///     Registers new guilds to which the bot has been invited while offline, and activates kept ones again
    /// </summary>
    private async Task RegisterNewGuildsAsync()
    {
        _logger.LogInformation("Registering new guilds");

        var stored = await GetStoredGuildsAsync();
        var connected = _client.Guilds.Select(g => g.Id).ToList();

        var newIds = connected.Where(id => !stored.ContainsKey(id)).ToList();
        foreach (var guildId in newIds)
        {
            _logger.LogInformation("New guild id {Id} found, registering", guildId);
            await _guildService.AddGuildAsync(guildId);
        }

        var inactiveIds = connected.Where(id => stored.TryGetValue(id, out var active) && !active).ToList();
        foreach (var guildId in inactiveIds)
        {
            _logger.LogInformation("Kept guild id {Id} is connected again, activating", guildId);
            await _guildService.UpdateGuildAsync(guildId, g => g.Active = true);
        }

        _logger.LogInformation("Registered {Count} new guilds, activated {Activated} kept guilds", newIds.Count,
            inactiveIds.Count);
    }

    /// <summary>
    ///     Removes guilds that the bot was kicked from while being offline
    /// </summary>
    private async Task RemoveLeftGuildsAsync()
    {
        _logger.LogInformation("Removing guilds that the bot is no longer connected to");

        if (_client.LoginState != LoginState.LoggedIn)
        {
            _logger.LogError("Guild removal interrupted because client is not logged in");
            return;
        }

        var connectedIds = _client.Guilds.Select(g => g.Id).ToHashSet();
        if (connectedIds.Count == 0)
        {
            _logger.LogWarning("Guild removal interrupted because connected guilds count is zero");
            return;
        }

        // Inactive guilds were already left, their data is kept on purpose
        var leftIds = (await GetStoredGuildsAsync())
            .Where(g => g.Value && !connectedIds.Contains(g.Key))
            .Select(g => g.Key)
            .ToList();

        foreach (var guildId in leftIds)
        {
            _logger.LogInformation("Guild ID {ID} was left", guildId);
            await HandleLeftAsync(guildId);
        }

        _logger.LogInformation("Handled {Count} left guilds", leftIds.Count);
    }

    /// <returns>Stored guild IDs with whether the guild is active</returns>
    private async Task<Dictionary<ulong, bool>> GetStoredGuildsAsync()
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        return await db.Guilds
            .Select(g => new { g.GuildId, Active = g.Active != false })
            .ToDictionaryAsync(g => g.GuildId, g => g.Active);
    }
}
