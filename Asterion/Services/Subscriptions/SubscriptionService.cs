using Asterion.Database;
using Asterion.Database.Models;
using Asterion.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Asterion.Services.Subscriptions;

public class SubscriptionService : ISubscriptionService
{
    private readonly ILogger<SubscriptionService> _logger;
    private readonly IServiceProvider _services;

    public SubscriptionService(IServiceProvider services, ILogger<SubscriptionService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task<AddSubscriptionResult> AddSubscriptionAsync(ulong guildId, string projectId,
        string lastCheckVersion, DateTime lastUpdated, ulong channelId, string? projectTitle = null,
        CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var guild = await db.Guilds.FirstOrDefaultAsync(g => g.GuildId == guildId, ct);
        if (guild is null)
        {
            _logger.LogError("No guild with ID {ID}", guildId);
            return AddSubscriptionResult.GuildNotFound;
        }

        if (await IsSubscribedAsync(db, guildId, projectId, ct))
            return AddSubscriptionResult.AlreadySubscribed;

        var project = await db.ModrinthProjects.FirstOrDefaultAsync(p => p.ProjectId == projectId, ct)
                      ?? db.ModrinthProjects.Add(new ModrinthProject
                      {
                          ProjectId = projectId,
                          Created = DateTime.Now,
                          LastUpdated = lastUpdated,
                          LastCheckVersion = lastCheckVersion,
                          Title = projectTitle
                      }).Entity;

        db.ModrinthEntries.Add(new ModrinthEntry
        {
            CustomUpdateChannel = channelId,
            Created = DateTime.Now,
            GuildId = guildId,
            ArrayId = guild.ModrinthArrayId,
            Project = project
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost the race to a concurrent subscribe, the unique index rejected the second entry
            if (await IsSubscribedAsync(db, guildId, projectId, ct))
                return AddSubscriptionResult.AlreadySubscribed;

            throw;
        }

        return AddSubscriptionResult.Added;
    }

    public async Task<ModrinthEntry?> RemoveSubscriptionAsync(ulong guildId, string projectId,
        CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var entry = await db.ModrinthEntries
            .Include(e => e.Project)
            .FirstOrDefaultAsync(e => e.GuildId == guildId && e.ProjectId == projectId, ct);

        if (entry is null)
            return null;

        db.ModrinthEntries.Remove(entry);
        await db.SaveChangesAsync(ct);

        await ProjectCleanup.RemoveUnusedProjectsAsync(db, [projectId], ct);

        return entry;
    }

    public async Task<int> RemoveAllSubscriptionsAsync(ulong guildId, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var entries = await db.ModrinthEntries.Where(e => e.GuildId == guildId).ToListAsync(ct);
        if (entries.Count == 0)
            return 0;

        db.ModrinthEntries.RemoveRange(entries);
        await db.SaveChangesAsync(ct);

        await ProjectCleanup.RemoveUnusedProjectsAsync(db, entries.Select(e => e.ProjectId), ct);

        return entries.Count;
    }

    public async Task<IList<ModrinthEntry>> GetSubscriptionsAsync(ulong guildId, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        return await db.ModrinthEntries
            .AsNoTracking()
            .Include(e => e.Project)
            .Where(e => e.GuildId == guildId)
            .ToListAsync(ct);
    }

    public async Task<ModrinthEntry?> GetSubscriptionAsync(ulong guildId, string projectId,
        CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        return await db.ModrinthEntries
            .AsNoTracking()
            .Include(e => e.Project)
            .FirstOrDefaultAsync(e => e.GuildId == guildId && e.ProjectId == projectId, ct);
    }

    public async Task<bool> IsSubscribedAsync(ulong guildId, string projectId, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        return await IsSubscribedAsync(db, guildId, projectId, ct);
    }

    private static Task<bool> IsSubscribedAsync(DataContext db, ulong guildId, string projectId,
        CancellationToken ct)
    {
        return db.ModrinthEntries.AnyAsync(e => e.GuildId == guildId && e.ProjectId == projectId, ct);
    }

    public async Task<ModrinthEntry?> UpdateSubscriptionAsync(ulong guildId, string projectId,
        Action<ModrinthEntry> update, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var entry = await db.ModrinthEntries
            .Include(e => e.Project)
            .FirstOrDefaultAsync(e => e.GuildId == guildId && e.ProjectId == projectId, ct);

        if (entry is null)
            return null;

        update(entry);
        await db.SaveChangesAsync(ct);

        return entry;
    }
}
