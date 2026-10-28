using Asterion.Database;
using Asterion.Database.Models;
using Asterion.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Array = Asterion.Database.Models.Array;

namespace Asterion.Services.Guilds;

public class GuildService : IGuildService
{
    private readonly ILogger<GuildService> _logger;
    private readonly IServiceProvider _services;

    public GuildService(IServiceProvider services, ILogger<GuildService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task<bool> AddGuildAsync(ulong guildId, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        if (await db.Guilds.AnyAsync(g => g.GuildId == guildId, ct))
        {
            _logger.LogInformation("Guild id {Id} is already registered in database", guildId);
            return false;
        }

        var array = new Array { Type = ArrayType.Modrinth };
        var settings = new GuildSettings();
        var guild = new Guild
        {
            GuildId = guildId,
            ModrinthArray = array,
            GuildSettings = settings,
            Created = DateTime.Now
        };

        settings.Guild = guild;
        array.Guild = guild;

        db.Guilds.Add(guild);
        await db.SaveChangesAsync(ct);

        return true;
    }

    public async Task<bool> RemoveGuildAsync(ulong guildId, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        // Loaded so they are deleted with the guild, not only by the database cascade
        var guild = await db.Guilds
            .Include(g => g.ModrinthArray)
            .Include(g => g.GuildSettings)
            .FirstOrDefaultAsync(g => g.GuildId == guildId, ct);

        if (guild is null)
        {
            _logger.LogError("No guild with ID {ID} found in database, guild removal interrupted", guildId);
            return false;
        }

        var entries = await db.ModrinthEntries.Where(e => e.GuildId == guildId).ToListAsync(ct);
        var follows = await db.ModrinthFollows.Include(f => f.Projects).Where(f => f.GuildId == guildId)
            .ToListAsync(ct);

        var projectIds = entries.Select(e => e.ProjectId)
            .Concat(follows.SelectMany(f => f.Projects).Select(fp => fp.ProjectId))
            .ToList();

        // Cascades to the follows' FollowProject rows and to the PendingNotification rows
        db.ModrinthEntries.RemoveRange(entries);
        db.ModrinthFollows.RemoveRange(follows);
        db.Guilds.Remove(guild);
        // The guild references its array, deleting the guild alone would leave the array behind
        db.Arrays.Remove(guild.ModrinthArray);
        await db.SaveChangesAsync(ct);

        await ProjectCleanup.RemoveUnusedProjectsAsync(db, projectIds, ct);

        return true;
    }

    public async Task<Guild?> GetGuildAsync(ulong guildId, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        return await db.Guilds
            .AsNoTracking()
            .Include(g => g.GuildSettings)
            .FirstOrDefaultAsync(g => g.GuildId == guildId, ct);
    }

    public async Task<Guild?> UpdateGuildAsync(ulong guildId, Action<Guild> update, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var guild = await db.Guilds
            .Include(g => g.GuildSettings)
            .FirstOrDefaultAsync(g => g.GuildId == guildId, ct);

        if (guild is null)
            return null;

        update(guild);
        await db.SaveChangesAsync(ct);

        return guild;
    }

    public async Task<ulong?> GetManageRoleIdAsync(ulong guildId, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        return await db.Guilds
            .Where(g => g.GuildId == guildId)
            .Select(g => g.ManageRole)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> MilestonesEnabledAsync(ulong guildId, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        return await db.GuildSettings
            .Where(s => s.GuildId == guildId)
            .Select(s => s.MilestonesEnabled)
            .FirstOrDefaultAsync(ct);
    }
}
