﻿using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Modrinth.RestClient.Models;
using Modrinth.RestClient.Models.Tags;
using Version = Modrinth.RestClient.Models.Version;

namespace RinthBot.Services.Modrinth;

public partial class ModrinthService
{
    /// <summary>
    /// Gets project by it's slug or ID, firstly it tries to retrieve it from cache, if it's not in cache,
    /// then it will use Modrinth's API and saves the result to cache
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
            p = await _api.GetProjectAsync(slugOrId);
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
            var searchResponse = await _api.GetProjectVersionListAsync(slugOrId);
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
            var searchResponse = await _api.GetMultipleVersionsAsync(versions);
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
        try
        {
            var team = await _api.GetProjectTeamMembersByProjectAsync(projectId);
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
            var searchResponse = await _api.SearchProjectsAsync(query);
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
            var searchResponse = await _api.GetMultipleProjectsAsync(projectIds);
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
            _logger.LogDebug("All game versions in cache");
            return versions;
        }

        try
        {
            _logger.LogDebug("Game versions not in cache, fetching from api...");
            var gameVersions = await _api.GetGameVersionsAsync();
            
            _cache.Set("gameVersions", gameVersions, absoluteExpirationRelativeToNow: TimeSpan.FromHours(1));

            return gameVersions;
        }
        catch (Exception e)
        {
            return null;
        }
    }
}