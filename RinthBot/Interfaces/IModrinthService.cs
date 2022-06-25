using Modrinth.RestClient.Models;

namespace RinthBot.Interfaces;

public interface IModrinthService
{
    public Task<Project?> GetProject(string slugOrId);
    public Task<SearchResponse?> SearchProjects(string query);
}