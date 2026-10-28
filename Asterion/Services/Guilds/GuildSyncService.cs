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
        _client.JoinedGuild += guild => _guildService.AddGuildAsync(guild.Id);
        _client.LeftGuild += guild => _guildService.RemoveGuildAsync(guild.Id);
        _client.Ready += SyncAsync;
    }

    private async Task SyncAsync()
    {
        await CreateMissingGuildSettingsAsync();
        await RegisterNewGuildsAsync();
        await RemoveLeftGuildsAsync();
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
    ///     Registers new guilds to which the bot has been invited while offline
    /// </summary>
    private async Task RegisterNewGuildsAsync()
    {
        _logger.LogInformation("Registering new guilds");

        var storedIds = await GetStoredGuildIdsAsync();
        var newGuilds = _client.Guilds.Where(g => !storedIds.Contains(g.Id)).ToList();

        foreach (var guild in newGuilds)
        {
            _logger.LogInformation("New guild id {Id} found, registering", guild.Id);
            await _guildService.AddGuildAsync(guild.Id);
        }

        _logger.LogInformation("Registered {Count} new guilds", newGuilds.Count);
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

        var leftIds = (await GetStoredGuildIdsAsync()).Where(id => !connectedIds.Contains(id)).ToList();

        foreach (var guildId in leftIds)
        {
            _logger.LogInformation("Removing guild ID {ID}", guildId);
            await _guildService.RemoveGuildAsync(guildId);
        }

        _logger.LogInformation("Removed {Count} guilds", leftIds.Count);
    }

    private async Task<HashSet<ulong>> GetStoredGuildIdsAsync()
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        return (await db.Guilds.Select(g => g.GuildId).ToListAsync()).ToHashSet();
    }
}
