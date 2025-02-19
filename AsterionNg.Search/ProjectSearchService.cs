namespace AsterionNg.Search;

public struct SearchResult
{
    public string ProviderName { get; set; }
    public IProject Project { get; set; }
}

public class ProjectSearchService
{
    private readonly Dictionary<string, IProjectSearchProvider> _providers;

    public ProjectSearchService(IEnumerable<IProjectSearchProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.ProviderName.ToLower());
    }

    public async Task<IEnumerable<IProject>> SearchAllAsync(string query, int limitPerProvider = 5)
    {
        var tasks = _providers.Values.Select(provider => 
            provider.SearchProjectsAsync(query, limitPerProvider));
        
        var results = await Task.WhenAll(tasks);
        return results.SelectMany(x => x);
    }
    
    public async Task<IEnumerable<IProject>> SearchProviderAsync(
        string providerName, 
        string query, 
        int limit = 10)
    {
        if (_providers.TryGetValue(providerName.ToLower(), out var provider))
        {
            return await provider.SearchProjectsAsync(query, limit);
        }
        
        throw new ArgumentException($"Provider '{providerName}' not found.");
    }

    public async Task<IProject> GetProjectAsync(string providerName, string projectId)
    {
        if (_providers.TryGetValue(providerName.ToLower(), out var provider))
        {
            return await provider.GetProjectByIdAsync(projectId);
        }
        
        throw new ArgumentException($"Provider '{providerName}' not found.");
    }
}