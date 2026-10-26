using Asterion.Common;
using Asterion.Database.Models;

namespace Asterion.Test.Follows;

[TestFixture]
public class SubscriptionInputTests
{
    [TestCase("sodium", "sodium")]
    [TestCase("  AANobbMI ", "AANobbMI")]
    [TestCase("https://modrinth.com/mod/sodium", "sodium")]
    [TestCase("modrinth.com/mod/sodium", "sodium")]
    public void ParsesProjectInput(string input, string expected)
    {
        Assert.That(Subscriptions.ParseProjectInput(input), Is.EqualTo(expected));
    }

    [TestCase("jellysquid3", "jellysquid3")]
    [TestCase("  jellysquid3 ", "jellysquid3")]
    [TestCase("https://modrinth.com/user/jellysquid3", "jellysquid3")]
    [TestCase("modrinth.com/user/jellysquid3/projects", "jellysquid3")]
    [TestCase("https://www.modrinth.com/user/jellysquid3?tab=mods", "jellysquid3")]
    public void ParsesUserInput(string input, string expected)
    {
        Assert.That(Subscriptions.ParseUserInput(input), Is.EqualTo(expected));
    }

    [TestCase("https://modrinth.com/mod/sodium")]
    [TestCase("modrinth.com/mod/sodium")]
    public void ProjectLinkIsNotAUser(string input)
    {
        Assert.That(Subscriptions.ParseUserInput(input), Is.Null);
    }

    [TestCase("u1")]
    [TestCase("JellySquid3")]
    public void MatchesFollowByIdOrName(string user)
    {
        var follows = new List<ModrinthFollow>
        {
            new() { Id = 7, TargetType = FollowTargetType.User, TargetId = "u1", TargetName = "jellysquid3" }
        };

        Assert.That(Subscriptions.MatchFollow(follows, user)?.Id, Is.EqualTo(7));
    }
}
