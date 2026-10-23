using Asterion.Attributes;
using Asterion.Common;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Asterion.AutocompleteHandlers;

public class AnnouncementChannelPrecondition : ParameterPreconditionAttribute
{
    public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
        IParameterInfo parameterInfo, object? value,
        IServiceProvider services)
    {
        if (value is null)
            // That's ok for us
            return Task.FromResult(PreconditionResult.FromSuccess());

        // We need to check if the channel is a text channel, because we can't send messages to voice channels
        if (value is not SocketTextChannel channel)
            return Task.FromResult<PreconditionResult>(LocalizedPreconditionResult.FromError(Errors.ChannelNotText));

        // We need to check if the bot can view and send messages to the channel
        var permissions = channel.Guild.CurrentUser.GetPermissions(channel);
        if (!permissions.SendMessages || !permissions.ViewChannel)
            return Task.FromResult<PreconditionResult>(
                LocalizedPreconditionResult.FromError(Errors.BotCannotAccessChannel(channel.Mention)));

        // All good
        return Task.FromResult(PreconditionResult.FromSuccess());
    }
}