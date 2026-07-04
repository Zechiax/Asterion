using Asterion.Database.Models;
using Asterion.Services.Notifications;
using Modrinth.Models;
using Modrinth.Models.Enums.Project;
using Version = Modrinth.Models.Version;

namespace Asterion.Test.Notifications;

[TestFixture]
public class NotificationFilterEvaluatorTests
{
    private static ModrinthEntry MakeEntry(ReleaseType releaseFilter, string[]? loaderFilter = null)
    {
        return new ModrinthEntry
        {
            EntryId = 1,
            ArrayId = 1,
            ProjectId = "project",
            GuildId = 1,
            Created = DateTime.UtcNow,
            ReleaseFilter = releaseFilter,
            LoaderFilter = loaderFilter
        };
    }

    private static Version MakeVersion(ProjectVersionType type, params string[] loaders)
    {
        return new Version
        {
            Id = "version",
            ProjectId = "project",
            ProjectVersionType = type,
            Loaders = loaders,
            DatePublished = DateTime.UtcNow
        };
    }

    [Test]
    public void AllowsReleaseWhenReleaseFilterIncludesIt()
    {
        var entry = MakeEntry(ReleaseType.Release);
        var version = MakeVersion(ProjectVersionType.Release);

        Assert.That(NotificationFilterEvaluator.ShouldNotify(entry, version), Is.True);
    }

    [Test]
    public void SkipsAlphaWhenReleaseFilterExcludesIt()
    {
        var entry = MakeEntry(ReleaseType.Release | ReleaseType.Beta);
        var version = MakeVersion(ProjectVersionType.Alpha);

        Assert.That(NotificationFilterEvaluator.ShouldNotify(entry, version), Is.False);
    }

    [Test]
    public void AllowsAnyLoaderWhenNoLoaderFilterSet()
    {
        var entry = MakeEntry(ReleaseType.Alpha | ReleaseType.Beta | ReleaseType.Release);
        var version = MakeVersion(ProjectVersionType.Release, "fabric");

        Assert.That(NotificationFilterEvaluator.ShouldNotify(entry, version), Is.True);
    }

    [Test]
    public void SkipsVersionWhenNoLoaderMatchesFilter()
    {
        var entry = MakeEntry(ReleaseType.Alpha | ReleaseType.Beta | ReleaseType.Release, new[] { "forge" });
        var version = MakeVersion(ProjectVersionType.Release, "fabric", "quilt");

        Assert.That(NotificationFilterEvaluator.ShouldNotify(entry, version), Is.False);
    }

    [Test]
    public void AllowsVersionWhenAnyLoaderMatchesFilter()
    {
        var entry = MakeEntry(ReleaseType.Alpha | ReleaseType.Beta | ReleaseType.Release, new[] { "forge", "fabric" });
        var version = MakeVersion(ProjectVersionType.Release, "fabric");

        Assert.That(NotificationFilterEvaluator.ShouldNotify(entry, version), Is.True);
    }
}
