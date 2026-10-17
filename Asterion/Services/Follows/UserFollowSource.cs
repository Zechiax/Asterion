using System.Net;
using Asterion.Database.Models;
using Microsoft.Extensions.Logging;
using Modrinth;
using Modrinth.Exceptions;

namespace Asterion.Services.Follows;

public class UserFollowSource : IFollowSource
{
    private readonly IModrinthClient _client;
    private readonly ILogger<UserFollowSource> _logger;

    public UserFollowSource(IModrinthClient client, ILogger<UserFollowSource> logger)
    {
        _client = client;
        _logger = logger;
    }

    public FollowTargetType TargetType => FollowTargetType.User;

    public async Task<FollowSourceResult> GetProjectsAsync(string targetId, CancellationToken ct = default)
    {
        try
        {
            var user = await _client.User.GetAsync(targetId, ct);
            var projects = await _client.User.GetProjectsAsync(user.Id, ct);

            return FollowSourceResult.Ok(projects, user.Username);
        }
        catch (ModrinthApiException e) when (e.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Followed user {UserId} was not found on Modrinth", targetId);
            return FollowSourceResult.Failed("User not found on Modrinth (deleted or banned)");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning(e, "Could not get projects of followed user {UserId}", targetId);
            return FollowSourceResult.Failed($"Modrinth API error: {e.Message}");
        }
    }
}
