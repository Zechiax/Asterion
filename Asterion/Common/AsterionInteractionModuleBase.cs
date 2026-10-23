using System.Globalization;
using Asterion.Interfaces;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Color = Discord.Color;

namespace Asterion.Common;

public class AsterionInteractionModuleBase(
    ILocalizationService localizationService,
    IGuildCultureProvider cultureProvider)
    : InteractionModuleBase<SocketInteractionContext>
{
    protected CultureInfo? CommandCultureInfo { get; set; }
    protected ILocalizationService LocalizationService { get; } = localizationService;

    /// <summary>
    ///     Sends an error or warning card, whether the interaction was responded to already
    /// </summary>
    /// <param name="card">The card to send</param>
    /// <param name="ephemeral">If only the user sees the card</param>
    protected Task SendStatusAsync(StatusCard card, bool ephemeral = true)
    {
        return InteractionResponder.SendAsync(Context.Interaction, card.Build(LocalizationService, CommandCultureInfo),
            ephemeral);
    }

    /// <summary>
    ///     If the bot can see and send messages in the guild's text channel
    /// </summary>
    protected bool BotCanSendIn(ulong channelId)
    {
        var channel = Context.Guild?.GetTextChannel(channelId);
        if (channel is null)
            return false;

        var permissions = Context.Guild!.CurrentUser.GetPermissions(channel);
        return permissions.ViewChannel && permissions.SendMessages;
    }

    /// <summary>
    ///     If the user can change the server settings, other management needs only the manage role
    /// </summary>
    protected bool IsAdministrator => Context.User is IGuildUser { GuildPermissions.Administrator: true };

    /// <summary>
    ///     If the manage panel may show the server tabs: the user is an administrator and the panel is ephemeral,
    ///     old <c>/list</c> messages were public
    /// </summary>
    protected bool CanManageServerHere => IsAdministrator && Context.Interaction switch
    {
        SocketMessageComponent component => IsEphemeral(component.Message),
        SocketModal { Message: { } message } => IsEphemeral(message),
        _ => true
    };

    private static bool IsEphemeral(IMessage message)
    {
        return message.Flags?.HasFlag(MessageFlags.Ephemeral) == true;
    }

    /// <summary>
    ///     Reloads the culture of the responses, e.g. after the guild's language was changed
    /// </summary>
    protected async Task RefreshCultureAsync()
    {
        cultureProvider.Invalidate(Context.Guild.Id);
        CommandCultureInfo = await cultureProvider.GetCultureAsync(Context.Interaction);
    }

    public override async Task BeforeExecuteAsync(ICommandInfo command)
    {
        CommandCultureInfo = await cultureProvider.GetCultureAsync(Context.Interaction);
        await base.BeforeExecuteAsync(command);
    }
}