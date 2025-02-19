namespace AsterionNg.Search;

public interface IProject
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string Author { get; }
    string ProjectUrl { get; }
    DateTime LastUpdated { get; }
    string? Version { get; }
}