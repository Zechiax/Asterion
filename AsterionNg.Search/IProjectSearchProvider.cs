namespace AsterionNg.Search;

public interface IProjectSearchProvider
{
    string ProviderName { get; }
    Task<IEnumerable<IProject>> SearchProjectsAsync(string query, int limit = 10);
    Task<IProject> GetProjectByIdAsync(string id);
}
