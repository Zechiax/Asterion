﻿using Modrinth.RestClient.Models;

namespace RinthBot.Services.Modrinth;

public class SearchResult<T>
{
    public SearchResult(T? payload, SearchStatus searchStatus)
    {
        Payload = payload;
        SearchStatus = searchStatus;
    }

    public SearchStatus SearchStatus { get; private set; }
    public T? Payload { get; private set; }
}

public struct UserDto
{
    public User User;
    public Project[] Projects;
}

public enum SearchStatus
{
    ApiDown,
    NoResult,
    UnknownError,
    FoundById,
    FoundBySearch
}