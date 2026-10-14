using Asterion.Database.Models;
using Asterion.Services.Follows;
using Modrinth.Models;

namespace Asterion.Test.Fakes;

internal class FakeFollowSource : IFollowSource
{
    public FollowSourceResult Result { get; set; } = FollowSourceResult.Ok([], "user");
    public int CallCount { get; private set; }

    public FollowTargetType TargetType => FollowTargetType.User;

    public void SetProjects(params Project[] projects) => Result = FollowSourceResult.Ok(projects, "user");

    public Task<FollowSourceResult> GetProjectsAsync(string targetId, CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(Result);
    }
}
