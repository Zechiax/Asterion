using Modrinth.RestClient.Models;

namespace Asterion.Extensions;

public static class TeamMemberExtensions
{
    public static TeamMember? GetOwner(this IEnumerable<TeamMember>? teamMembers)
    {
        return (teamMembers ?? Array.Empty<TeamMember>()).FirstOrDefault(x =>
            string.Equals(x.Role, "owner", StringComparison.InvariantCultureIgnoreCase));
    }
    
    public static TeamMember? GetByUserId(this IEnumerable<TeamMember>? teamMembers, string userId)
    {
        return (teamMembers ?? Array.Empty<TeamMember>()).FirstOrDefault(x =>
            string.Equals(x.User.Id, userId));
    }
}