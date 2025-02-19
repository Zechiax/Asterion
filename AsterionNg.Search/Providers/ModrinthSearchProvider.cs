using Modrinth;

namespace AsterionNg.Search.Providers;

public class ModrinthProject : IProject
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Author { get; set; }
    public required string ProjectUrl { get; set; }
    public DateTime LastUpdated { get; set; }
    public string? Version { get; set; }
    
    // Additional Modrinth-specific properties
    public string[] Categories { get; set; }
    public int Downloads { get; set; }
}


public class ModrinthSearchProvider(ModrinthClient httpClient) : IProjectSearchProvider
{
    private readonly ModrinthClient _client = httpClient;
    private const string BaseUrl = "https://api.modrinth.com/v2";

    public string ProviderName => "Modrinth";

    public async Task<IEnumerable<IProject>> SearchProjectsAsync(string query, int limit = 10)
    {
        var response = await _client.Project.SearchAsync(query, limit: limit);
            
        return response.Hits.Select(hit => new ModrinthProject
        {
            Id = hit.ProjectId,
            Name = hit.Title,
            Description = hit.Description,
            Author = hit.Author,
            ProjectUrl = hit.Url,
            LastUpdated = hit.DateModified,
        });
    }

    public async Task<IProject> GetProjectByIdAsync(string id)
    {
        var project = await _client.Project.GetAsync(id);
        
        return new ModrinthProject
        {
            Id = project.Id,
            Name = project.Title,
            Description = project.Description,
            Author = project.Team,
            ProjectUrl = project.Url,
            LastUpdated = project.Updated,
            Version = project.Versions.FirstOrDefault() ?? "Unknown",
        };
    }
}