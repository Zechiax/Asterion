using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Asterion.AutocompleteHandlers;

public class AnnouncementChannelPrecondition : ParameterPreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
        IParameterInfo parameterInfo, object? value,
        IServiceProvider services)
    {
        if (value is null)
            // That's ok for us
            return PreconditionResult.FromSuccess();

        // We need to check if the channel is a text channel, because we can't send messages to voice channels
        if (value is not SocketTextChannel channel)
            return PreconditionResult.FromError("Channel must be a text channel");

        // We need to check if the bot has permissions to send messages to the channel
        if (channel.Guild.CurrentUser.GetPermissions(channel).SendMessages == false)
            return PreconditionResult.FromError("Bot doesn't have permissions to send messages to the channel");

        if (channel.Guild.CurrentUser.GetPermissions(channel).ViewChannel == false)
            return PreconditionResult.FromError("Bot doesn't have permissions to view the channel");

        // All good
        return PreconditionResult.FromSuccess();
    }
}