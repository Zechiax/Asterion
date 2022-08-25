using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using RinthBot.Services;

namespace RinthBot.Attributes;

public class DoManageSubsRoleCheck : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        if (context.User is not IGuildUser guildUser)
            return PreconditionResult.FromError("Command must be used in a guild channel.");
        
        var data = services.GetRequiredService<DataService>();

        var roleId = await data.GetManageRoleIdAsync(context.Guild.Id);

        if (roleId.HasValue == false)
        {
            return PreconditionResult.FromError("No Manage role has been set");
        }

        if (guildUser.RoleIds.Contains(roleId.Value))
        {
            return PreconditionResult.FromSuccess();
        }

        return PreconditionResult.FromError("User doesn't have permission");
    }
}