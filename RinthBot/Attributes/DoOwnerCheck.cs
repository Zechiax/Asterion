using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RinthBot.Attributes;

public class DoOwnerCheck : PreconditionAttribute
{
    public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        // Check if the component matches the target properly.
        //if (context.Interaction is not SocketMessageComponent)
        //    return Task.FromResult(PreconditionResult.FromError("Context unrecognized as component context."));
        
        var config = services.GetRequiredService<IConfiguration>();
        
        var owner = config.GetSection("client").GetValue<ulong>("owner");

        if (owner == context.User.Id)
            return Task.FromResult(PreconditionResult.FromSuccess());

        return Task.FromResult(PreconditionResult.FromError("Only the owners of this bot can use this command"));
    }
}