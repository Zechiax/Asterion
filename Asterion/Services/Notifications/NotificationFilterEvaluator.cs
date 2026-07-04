using Asterion.Database.Models;
using Modrinth.Models;
using Modrinth.Models.Enums.Project;
using Version = Modrinth.Models.Version;

namespace Asterion.Services.Notifications;

public static class NotificationFilterEvaluator
{
    public static bool ShouldNotify(ModrinthEntry entry, Version version)
    {
        var releaseFilter = entry.ReleaseFilter;

        switch (version.ProjectVersionType)
        {
            case ProjectVersionType.Alpha when !releaseFilter.HasFlag(ReleaseType.Alpha):
            case ProjectVersionType.Beta when !releaseFilter.HasFlag(ReleaseType.Beta):
            case ProjectVersionType.Release when !releaseFilter.HasFlag(ReleaseType.Release):
                return false;
        }

        if (entry.LoaderFilter is { Length: > 0 } loaderFilter && !version.Loaders.Any(loaderFilter.Contains))
            return false;

        return true;
    }
}
