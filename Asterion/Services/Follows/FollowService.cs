using System.Collections.Concurrent;
using Asterion.Database;
using Asterion.Database.Models;
using Asterion.Extensions;
using Asterion.Interfaces;
using Asterion.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modrinth;
using Modrinth.Models;
using Modrinth.Models.Enums.Project;
using Version = Modrinth.Models.Version;

namespace Asterion.Services.Follows;

public class FollowService : IFollowService
{
    private const int SplitSize = 100;

    /// <summary>
    ///     A project only counts as new if it was published after the last successful sync (minus this margin).
    ///     This keeps a project that left the target's list and came back from being announced again.
    /// </summary>
    private static readonly TimeSpan AnnounceGracePeriod = TimeSpan.FromDays(1);

    private static readonly ProjectStatus[] PublicStatuses =
        [ProjectStatus.Approved, ProjectStatus.Archived, ProjectStatus.Unlisted];

    /// <summary>
    ///     How long the loaders of a target are reused for the settings panel, the sync refreshes them as often
    /// </summary>
    private static readonly TimeSpan LoaderCacheDuration = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<(FollowTargetType, string), (DateTime At, IReadOnlyList<string> Loaders)>
        _loaderCache = new();

    /// <summary>
    ///     Serializes syncs, so the first sync of a new follow and the background pass don't add the same rows
    /// </summary>
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private readonly IModrinthClient _client;
    private readonly ILogger<FollowService> _logger;
    private readonly INotificationScheduler _notificationScheduler;
    private readonly INotificationSignal _notificationSignal;
    private readonly IServiceProvider _services;
    private readonly IReadOnlyDictionary<FollowTargetType, IFollowSource> _sources;

    public FollowService(IServiceProvider services, IModrinthClient client, IEnumerable<IFollowSource> sources,
        INotificationScheduler notificationScheduler, INotificationSignal notificationSignal,
        ILogger<FollowService> logger)
    {
        _services = services;
        _client = client;
        _sources = sources.ToDictionary(s => s.TargetType);
        _notificationScheduler = notificationScheduler;
        _notificationSignal = notificationSignal;
        _logger = logger;
    }

    public async Task<AddFollowResult> AddFollowAsync(ulong guildId, FollowTargetType targetType, string targetId,
        string targetName, ulong? channelId, FollowEvents events, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        if (!await db.Guilds.AnyAsync(g => g.GuildId == guildId, ct))
        {
            _logger.LogError("No guild with ID {Id}", guildId);
            return new AddFollowResult(AddFollowStatus.GuildNotFound);
        }

        var follows = await db.ModrinthFollows.Where(f => f.GuildId == guildId).ToListAsync(ct);

        if (follows.Any(f => f.TargetType == targetType && f.TargetId == targetId))
            return new AddFollowResult(AddFollowStatus.AlreadyFollowing);

        if (follows.Count >= IFollowService.MaxFollowsPerGuild)
            return new AddFollowResult(AddFollowStatus.LimitReached);

        var follow = new ModrinthFollow
        {
            GuildId = guildId,
            TargetType = targetType,
            TargetId = targetId,
            TargetName = targetName,
            Events = events,
            CustomUpdateChannel = channelId,
            Created = DateTime.UtcNow
        };

        db.ModrinthFollows.Add(follow);
        await db.SaveChangesAsync(ct);

        // The first sync only learns the current projects, it must not announce them
        var sync = await SyncFollowAsync(follow.Id, announceNew: false, ct);

        return new AddFollowResult(AddFollowStatus.Added, follow, sync);
    }

    public async Task<ModrinthFollow?> RemoveFollowAsync(ulong guildId, int followId, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var follow = await db.ModrinthFollows
            .Include(f => f.Projects)
            .FirstOrDefaultAsync(f => f.Id == followId && f.GuildId == guildId, ct);

        if (follow is null)
            return null;

        var projectIds = follow.Projects.Select(fp => fp.ProjectId).ToList();

        // Cascades to the follow's FollowProject and PendingNotification rows
        db.ModrinthFollows.Remove(follow);
        await db.SaveChangesAsync(ct);

        await ProjectCleanup.RemoveUnusedProjectsAsync(db, projectIds, ct);

        return follow;
    }

    public async Task<int> CountFollowsAsync(ulong guildId, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        return await db.ModrinthFollows.CountAsync(f => f.GuildId == guildId, ct);
    }

    public async Task<bool> SetIgnoredAsync(ulong guildId, int followId, string projectId, bool ignored,
        CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var followProject = await db.FollowProjects
            .FirstOrDefaultAsync(fp => fp.FollowId == followId && fp.ProjectId == projectId
                                                               && fp.Follow.GuildId == guildId, ct);

        if (followProject is null)
            return false;

        followProject.Ignored = ignored;
        await db.SaveChangesAsync(ct);

        return true;
    }

    public async Task<IList<ModrinthFollow>> GetFollowsAsync(ulong guildId, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        return await db.ModrinthFollows
            .AsNoTracking()
            .Include(f => f.Projects).ThenInclude(fp => fp.Project)
            .Where(f => f.GuildId == guildId)
            .OrderBy(f => f.Id)
            .ToListAsync(ct);
    }

    public async Task<ModrinthFollow?> GetFollowAsync(ulong guildId, int followId, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        return await db.ModrinthFollows
            .AsNoTracking()
            .Include(f => f.Projects).ThenInclude(fp => fp.Project)
            .FirstOrDefaultAsync(f => f.Id == followId && f.GuildId == guildId, ct);
    }

    public async Task<ModrinthFollow?> UpdateSettingsAsync(ulong guildId, int followId, Action<ModrinthFollow> update,
        CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var follow = await db.ModrinthFollows
            .FirstOrDefaultAsync(f => f.Id == followId && f.GuildId == guildId, ct);

        if (follow is null)
            return null;

        update(follow);
        await db.SaveChangesAsync(ct);

        return follow;
    }

    public async Task<IList<ModrinthFollow>> GetAllFollowsAsync(CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        return await db.ModrinthFollows.AsNoTracking().ToListAsync(ct);
    }

    public Task<FollowSourceResult> FetchTargetAsync(FollowTargetType targetType, string targetId,
        CancellationToken ct = default)
    {
        if (!_sources.TryGetValue(targetType, out var source))
            return Task.FromResult(FollowSourceResult.Failed($"Following of {targetType} targets is not supported"));

        return source.GetProjectsAsync(targetId, ct);
    }

    public async Task<SyncResult?> SyncFollowAsync(int followId, bool announceNew, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var follow = await db.ModrinthFollows.AsNoTracking().FirstOrDefaultAsync(f => f.Id == followId, ct);
        if (follow is null)
            return null;

        var source = await FetchTargetAsync(follow.TargetType, follow.TargetId, ct);
        return await SyncFollowAsync(followId, source, announceNew, ct);
    }

    public async Task<IReadOnlyList<string>> GetTargetLoadersAsync(FollowTargetType targetType, string targetId,
        CancellationToken ct = default)
    {
        var cached = _loaderCache.TryGetValue((targetType, targetId), out var entry);
        if (cached && DateTime.UtcNow - entry.At < LoaderCacheDuration)
            return entry.Loaders;

        var source = await FetchTargetAsync(targetType, targetId, ct);
        if (source.Success)
            return CacheLoaders(targetType, targetId, source);

        // Stale loaders are better than none while Modrinth is unavailable
        return cached ? entry.Loaders : [];
    }

    private IReadOnlyList<string> CacheLoaders(FollowTargetType targetType, string targetId, FollowSourceResult source)
    {
        var loaders = source.Projects!
            .SelectMany(p => p.Loaders ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _loaderCache[(targetType, targetId)] = (DateTime.UtcNow, loaders);
        return loaders;
    }

    public async Task<SyncResult?> SyncFollowAsync(int followId, FollowSourceResult source, bool announceNew,
        CancellationToken ct = default)
    {
        await _syncLock.WaitAsync(ct);
        try
        {
            return await SyncFollowLockedAsync(followId, source, announceNew, ct);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task<SyncResult?> SyncFollowLockedAsync(int followId, FollowSourceResult source, bool announceNew,
        CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var follow = await db.ModrinthFollows
            .Include(f => f.Projects)
            .FirstOrDefaultAsync(f => f.Id == followId, ct);

        if (follow is null)
            return null;

        // Never remove data after a failed call
        if (!source.Success)
            return await SaveSyncErrorAsync(db, follow, source.Error ?? "Unknown error", ct);

        CacheLoaders(follow.TargetType, follow.TargetId, source);

        var current = source.Projects!
            .Where(p => PublicStatuses.Contains(p.Status))
            .DistinctBy(p => p.Id)
            .ToDictionary(p => p.Id);

        var known = follow.Projects.ToDictionary(fp => fp.ProjectId);

        // The user left the team, transferred the project, or the project is no longer public
        var removed = known.Values.Where(fp => !current.ContainsKey(fp.ProjectId)).ToList();

        // A project with no versions is skipped, the next sync tries again
        var addedCandidates = current.Values
            .Where(p => !known.ContainsKey(p.Id) && p.Versions is { Length: > 0 })
            .ToList();

        var addedIds = addedCandidates.Select(p => p.Id).ToList();
        var existingProjects = await db.ModrinthProjects
            .Where(p => addedIds.Contains(p.ProjectId))
            .ToDictionaryAsync(p => p.ProjectId, ct);

        var announceEnabled = announceNew && follow.Events.HasFlag(FollowEvents.NewProject);
        var toAnnounce = addedCandidates
            .Where(p => announceEnabled && IsNewForFollow(follow, p))
            .Select(p => p.Id)
            .ToHashSet();

        // Versions are only needed to create missing project rows and for the new project embed
        var needVersions = addedCandidates
            .Where(p => !existingProjects.ContainsKey(p.Id) || toAnnounce.Contains(p.Id))
            .ToList();

        IReadOnlyDictionary<string, Version> latestVersions;
        try
        {
            latestVersions = await GetLatestVersionsAsync(needVersions, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning(e, "Could not get versions for follow {FollowId}", follow.Id);
            return await SaveSyncErrorAsync(db, follow, $"Modrinth API error: {e.Message}", ct);
        }

        db.FollowProjects.RemoveRange(removed);

        var now = DateTime.UtcNow;
        var added = new List<string>();
        var announced = new List<string>();

        foreach (var project in addedCandidates)
        {
            latestVersions.TryGetValue(project.Id, out var latestVersion);
            var hasRow = existingProjects.ContainsKey(project.Id);
            var announce = toAnnounce.Contains(project.Id);

            if (latestVersion is null && (!hasRow || announce))
                continue;

            if (!hasRow)
                db.ModrinthProjects.Add(new ModrinthProject
                {
                    ProjectId = project.Id,
                    Title = project.Title,
                    LastCheckVersion = latestVersion!.Id,
                    LastUpdated = project.Updated,
                    Created = now
                });

            db.FollowProjects.Add(new FollowProject
            {
                FollowId = follow.Id,
                ProjectId = project.Id,
                FirstSeen = now
            });
            added.Add(project.Id);

            if (announce)
            {
                _notificationScheduler.EnqueueNewProject(db, project, latestVersion!, follow);
                announced.Add(project.Id);
            }
        }

        if (!string.IsNullOrEmpty(source.TargetName))
            follow.TargetName = source.TargetName;

        follow.LastSyncedAt = now;
        follow.LastSyncError = null;

        // Single atomic commit: followed projects, project rows and outbox rows land together
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e)
        {
            // E.g. a subscription added the same project row meanwhile, nothing was saved, the next sync retries
            _logger.LogWarning(e, "Could not save sync of follow {FollowId}", follow.Id);
            return new SyncResult(false, "Database conflict, the next sync retries", known.Count, [], [], []);
        }

        var removedIds = removed.Select(fp => fp.ProjectId).ToList();
        if (removedIds.Count > 0)
            await ProjectCleanup.RemoveUnusedProjectsAsync(db, removedIds, ct);

        if (added.Count > 0 || removedIds.Count > 0)
            _logger.LogInformation(
                "Synced follow {FollowId} of {TargetType} {TargetId}: {Added} added, {Removed} removed, {Announced} announced",
                follow.Id, follow.TargetType, follow.TargetId, added.Count, removedIds.Count, announced.Count);

        if (announced.Count > 0)
            _notificationSignal.Signal();

        var trackedCount = known.Count - removed.Count + added.Count;
        return new SyncResult(true, null, trackedCount, added, removedIds, announced);
    }

    private static async Task<SyncResult> SaveSyncErrorAsync(DataContext db, ModrinthFollow follow, string error,
        CancellationToken ct)
    {
        follow.LastSyncError = error;
        await db.SaveChangesAsync(ct);

        return new SyncResult(false, error, follow.Projects.Count, [], [], []);
    }

    /// <summary>
    ///     A project is new for the follow if it was published after the follow was created, and after the
    ///     last successful sync. The first rule keeps old projects from being announced when the user becomes
    ///     a maintainer of them, the second keeps a returning project from being announced again.
    /// </summary>
    private static bool IsNewForFollow(ModrinthFollow follow, Project project)
    {
        var publishedAt = ToUtc(project.Approved ?? project.Published);

        if (publishedAt <= follow.Created)
            return false;

        return follow.LastSyncedAt is not { } lastSynced || publishedAt > lastSynced - AnnounceGracePeriod;
    }

    private static DateTime ToUtc(DateTime dateTime)
    {
        return dateTime.Kind == DateTimeKind.Local ? dateTime.ToUniversalTime() : dateTime;
    }

    /// <summary>
    ///     Returns the newest version of each project, projects with no returned versions are missing
    /// </summary>
    private async Task<IReadOnlyDictionary<string, Version>> GetLatestVersionsAsync(IList<Project> projects,
        CancellationToken ct)
    {
        var versionIds = projects.SelectMany(p => p.Versions).Distinct().ToArray();
        var versions = new List<Version>();

        foreach (var segment in versionIds.Split(SplitSize))
            versions.AddRange(await _client.Version.GetMultipleAsync(segment, ct));

        return versions
            .GroupBy(v => v.ProjectId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.DatePublished).First());
    }
}
