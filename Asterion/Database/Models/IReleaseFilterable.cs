namespace Asterion.Database.Models;

/// <summary>
///     Anything that carries release-type and loader filters for version update notifications.
/// </summary>
public interface IReleaseFilterable
{
    ReleaseType ReleaseFilter { get; }
    string[]? LoaderFilter { get; }
}
