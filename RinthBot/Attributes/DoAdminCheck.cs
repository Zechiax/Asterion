using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RinthBot.Attributes;

public class DoAdminCheck : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        // Check if the component matches the target properly.
        //if (context.Interaction is not SocketMessageComponent)
        //    return Task.FromResult(PreconditionResult.FromError("Context unrecognized as component context."));

        var guildUser = await context.Guild.GetUserAsync(context.User.Id);
        
        if (guildUser.GuildPermissions.Administrator)
        {
            return PreconditionResult.FromSuccess();
        }
        
        return PreconditionResult.FromError("Only Admin can use this command");
    }
}