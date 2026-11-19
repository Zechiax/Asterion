using Asterion.Database.Models;
using Modrinth.Models;
using Modrinth.Models.Enums.Project;
using Version = Modrinth.Models.Version;

namespace Asterion.Services.Notifications;

public static class NotificationFilterEvaluator
{
    public static bool ShouldNotify(IReleaseFilterable entry, Version version, GuildSettings? guildSettings = null)
    {
        var releaseFilter = entry.ReleaseFilter ?? guildSettings?.DefaultReleaseFilter ?? GuildSettings.AllReleaseTypes;

        switch (version.ProjectVersionType)
        {
            case ProjectVersionType.Alpha when !releaseFilter.HasFlag(ReleaseType.Alpha):
            case ProjectVersionType.Beta when !releaseFilter.HasFlag(ReleaseType.Beta):
            case ProjectVersionType.Release when !releaseFilter.HasFlag(ReleaseType.Release):
                return false;
        }

        // Empty array on the entry overrides the guild default to allow all loaders; null inherits the guild default
        var loaderFilter = entry.LoaderFilter ?? guildSettings?.DefaultLoaderFilter;
        if (loaderFilter is { Length: > 0 } &&
            !version.Loaders.Any(l => loaderFilter.Contains(l, StringComparer.OrdinalIgnoreCase)))
            return false;

        return true;
    }
}
