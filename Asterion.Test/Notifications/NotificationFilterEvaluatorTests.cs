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

    private const ReleaseType AllReleases = ReleaseType.Alpha | ReleaseType.Beta | ReleaseType.Release;

    [TestCase(ReleaseType.Release, ProjectVersionType.Release, null, true)]
    [TestCase(ReleaseType.Release | ReleaseType.Beta, ProjectVersionType.Alpha, null, false)]
    // No loader filter allows any loader
    [TestCase(AllReleases, ProjectVersionType.Release, null, true)]
    [TestCase(AllReleases, ProjectVersionType.Release, new[] { "forge" }, false)]
    [TestCase(AllReleases, ProjectVersionType.Release, new[] { "forge", "fabric" }, true)]
    public void ShouldNotify(ReleaseType releaseFilter, ProjectVersionType versionType, string[]? loaderFilter,
        bool expected)
    {
        var entry = MakeEntry(releaseFilter, loaderFilter);
        var version = MakeVersion(versionType, "fabric", "quilt");

        Assert.That(NotificationFilterEvaluator.ShouldNotify(entry, version), Is.EqualTo(expected));
    }
}
