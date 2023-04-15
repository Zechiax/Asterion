using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Modrinth.Models;
using Modrinth.Models.Tags;
using Version = Modrinth.Models.Version;

namespace Asterion.Services.Modrinth;

public partial class ModrinthService
{
    /// <summary>
    ///     Gets project by it's slug or ID, firstly it tries to retrieve it from cache, if it's not in cache,
    ///     then it will use Modrinth's API and saves the result to cache
    /// </summary>
    /// <param name="slugOrId"></param>
    /// <returns></returns>
    public async Task<Project?> GetProject(string slugOrId)
    {
        if (_cache.TryGetValue(slugOrId, out var value) && value is Project project)
        {
            _logger.LogDebug("{Id} retrieved from cache", slugOrId);
            return project;
        }

        _logger.LogDebug("{Id} not in cache", slugOrId);
        Project? p;
        try
        {
            p = await Api.Project.GetAsync(slugOrId);
        }
        catch (Exception e)
        {
            _logger.LogWarning("{ExceptionMessage}", e.Message);
            return null;
        }

        _logger.LogDebug("Saving {Id} to cache", slugOrId);
        _cache.Set(slugOrId, p, _cacheEntryOptions);

        return p;
    }

    public async Task<Version[]?> GetVersionListAsync(string slugOrId)
    {
        try
        {
            var searchResponse = await Api.Version.GetProjectVersionListAsync(slugOrId);
            return searchResponse;
        }
        catch (Exception e)
        {
            _logger.LogWarning("{ExceptionMessage}", e.Message);
        }

        return null;
    }

    public async Task<Version[]?> GetMultipleVersionsAsync(IEnumerable<string> versions)
    {
        try
        {
            var searchResponse = await Api.Version.GetMultipleAsync(versions);
            return searchResponse;
        }
        catch (Exception e)
        {
            _logger.LogWarning("{ExceptionMessage}", e.Message);
        }

        return null;
    }

    public async Task<TeamMember[]?> GetProjectsTeamMembersAsync(string projectId)
    {
        if (_cache.TryGetValue($"project-team-members:{projectId}", out var value) &&
            value is TeamMember[] teamMembers)
        {
            _logger.LogDebug("Team members for project ID {ProjectId} are in cache", projectId);
            return teamMembers;
        }


        try
        {
            _logger.LogDebug("Team members for project ID {ProjectId} are not in cache", projectId);
            var team = await Api.Team.GetProjectTeamAsync(projectId);

            _cache.Set($"project-team-members:{projectId}", team, TimeSpan.FromMinutes(30));
            _logger.LogDebug("Saving team members for project ID {ProjectId} to cache", projectId);
            return team;
        }
        catch (Exception e)
        {
            _logger.LogWarning("{ExceptionMessage}", e.Message);
        }

        return null;
    }

    public async Task<SearchResponse?> SearchProjects(string query)
    {
        try
        {
            var searchResponse = await Api.Project.SearchAsync(query);
            return searchResponse;
        }
        catch (Exception e)
        {
            _logger.LogWarning("{ExceptionMessage}", e.Message);
        }

        return null;
    }

    public async Task<Project[]?> GetMultipleProjects(IEnumerable<string> projectIds)
    {
        try
        {
            var searchResponse = await Api.Project.GetMultipleAsync(projectIds);
            return searchResponse;
        }
        catch (Exception e)
        {
            _logger.LogWarning("{ExceptionMessage}", e.Message);
        }

        return null;
    }

    public async Task<Version?> GetProjectsLatestVersion(string projectId)
    {
        var versions = await GetVersionListAsync(projectId);

        // Get last version ID
        var lastVersion = versions?.OrderByDescending(x => x.DatePublished).First();

        return lastVersion;
    }

    public async Task<Version?> GetProjectsLatestVersion(Project project)
    {
        return await GetProjectsLatestVersion(project.Id);
    }

    public async Task<GameVersion[]?> GetGameVersions()
    {
        if (_cache.TryGetValue("gameVersions", out var value) && value is GameVersion[] versions)
        {
            _logger.LogDebug("Fetched game version list from cache");
            return versions;
        }

        try
        {
            _logger.LogDebug("Game versions not in cache, fetching from api...");
            var gameVersions = await Api.Tag.GetGameVersionsAsync();

            _cache.Set("gameVersions", gameVersions, TimeSpan.FromHours(12));

            _logger.LogDebug("Game versions set in cache");
            return gameVersions;
        }
        catch (Exception e)
        {
            _logger.LogWarning("Could not get list of game versions: {Exception}", e.Message);
            return null;
        }
    }
}