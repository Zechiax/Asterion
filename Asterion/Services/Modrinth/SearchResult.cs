﻿using System.Drawing;
using Modrinth.RestClient.Models;

namespace Asterion.Services.Modrinth;

public class SearchResult<T>
{
    public SearchResult(T? payload, SearchStatus searchStatus)
    {
        Payload = payload;
        SearchStatus = searchStatus;
        SearchTime = DateTime.UtcNow;
    }
    /// <summary>
    /// Success in getting results
    /// </summary>
    public bool Success => SearchStatus is SearchStatus.FoundById or SearchStatus.FoundBySearch;
    public DateTime SearchTime { get; private set; }
    public SearchStatus SearchStatus { get; private set; }
    public T? Payload { get; private set; }
}

public struct UserDto
{
    public User User;
    public Project[] Projects;
    public Discord.Color MajorColor;
}

public struct ProjectDto
{
    public Project Project;
    public Discord.Color MajorColor;
    public SearchResponse? SearchResponse;
}

public enum SearchStatus
{
    ApiDown,
    NoResult,
    UnknownError,
    FoundById,
    FoundBySearch
}