using System.Drawing;
using Modrinth.RestClient.Models;

namespace RinthBot.Services.Modrinth;

public class SearchResult<T>
{
    public SearchResult(T? payload, SearchStatus searchStatus)
    {
        Payload = payload;
        SearchStatus = searchStatus;
        SearchTime = DateTime.UtcNow;
    }

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

public enum SearchStatus
{
    ApiDown,
    NoResult,
    UnknownError,
    FoundById,
    FoundBySearch
}