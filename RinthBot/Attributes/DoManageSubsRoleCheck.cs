using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using RinthBot.Services;

namespace RinthBot.Attributes;

public class DoManageSubsRoleCheck : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        // Guild only precondition
        if (context.User is not IGuildUser guildUser)
            return PreconditionResult.FromError("Command must be used in a guild channel.");
        
        var data = services.GetRequiredService<DataService>();

        // Get manage role id
        var roleId = await data.GetManageRoleIdAsync(context.Guild.Id);

        // Check if it has been set
        if (roleId.HasValue == false)
        {
            return PreconditionResult.FromError("No Manage role has been set");
        }

        // If set and the user have this role, precondition is successful 
        if (guildUser.RoleIds.Contains(roleId.Value))
        {
            return PreconditionResult.FromSuccess();
        }

        return PreconditionResult.FromError("User doesn't have permission");
    }
}