using Asterion.Database;
using Asterion.Database.Models;
using Asterion.Services.Follows;
using Asterion.Services.Notifications;
using Asterion.Test.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Modrinth;
using Modrinth.Models;
using Modrinth.Models.Enums.Project;
using NSubstitute;
using Array = Asterion.Database.Models.Array;
using Version = Modrinth.Models.Version;

namespace Asterion.Test.Follows;

internal static class FollowTestData
{
    public const ulong GuildId = 1;
    public const string UserId = "user-1";

    public static async Task SeedGuildAsync(IServiceProvider provider, ulong guildId = GuildId)
    {
        using var scope = provider.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var guild = new Guild { GuildId = guildId, Created = DateTime.UtcNow };
        var settings = new GuildSettings { GuildId = guildId, Guild = guild };
        var array = new Array { ArrayId = guildId, GuildId = guildId, Guild = guild, Type = ArrayType.Modrinth };

        db.Guilds.Add(guild);
        db.GuildSettings.Add(settings);
        db.Arrays.Add(array);
        await db.SaveChangesAsync();
    }

    /// <summary>A public project with one version named "{id}-v1", approved at <paramref name="approved" />.</summary>
    public static Project MakeProject(string id, DateTime approved, ProjectStatus status = ProjectStatus.Approved,
        bool hasVersions = true)
    {
        return new Project
        {
            Id = id,
            Slug = id,
            Title = $"Project {id}",
            Description = "Description",
            Team = $"team-{id}",
            Status = status,
            Published = approved,
            Approved = approved,
            Updated = approved,
            Versions = hasVersions ? [$"{id}-v1"] : [],
            Categories = [],
            Loaders = ["fabric"],
            Gallery = []
        };
    }

    public static Version MakeVersion(string id, string projectId, DateTime published)
    {
        return new Version
        {
            Id = id,
            ProjectId = projectId,
            VersionNumber = "1.0.0",
            ProjectVersionType = ProjectVersionType.Release,
            Loaders = ["fabric"],
            GameVersions = ["1.20.1"],
            Files = [],
            DatePublished = published
        };
    }

    /// <summary>
    ///     A client whose Version.GetMultipleAsync returns a "{projectId}-v1" version for each requested id
    ///     of the form "{projectId}-v1".
    /// </summary>
    public static IModrinthClient MakeClient()
    {
        var client = Substitute.For<IModrinthClient>();
        client.Version.GetMultipleAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IEnumerable<string>>()
                .Select(id => MakeVersion(id, id[..id.LastIndexOf("-v", StringComparison.Ordinal)],
                    DateTime.UtcNow.AddDays(-1)))
                .ToArray());
        return client;
    }

    public static FollowService BuildService(IServiceProvider provider, FakeFollowSource source,
        IModrinthClient? client = null)
    {
        return new FollowService(provider, client ?? MakeClient(), [source], new NotificationScheduler(),
            new NotificationSignal(), NullLogger<FollowService>.Instance);
    }
}
