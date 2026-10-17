using System.Globalization;
using Asterion.EmbedBuilders;
using Asterion.Interfaces;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Color = Discord.Color;

namespace Asterion.Common;

public class AsterionInteractionModuleBase : InteractionModuleBase<SocketInteractionContext>
{
    protected CultureInfo? CommandCultureInfo { get; set; }
    protected ILocalizationService LocalizationService { get; }
    private readonly IGuildCultureProvider _cultureProvider;

    public AsterionInteractionModuleBase(ILocalizationService localizationService, IGuildCultureProvider cultureProvider)
    {
        LocalizationService = localizationService;
        _cultureProvider = cultureProvider;
    }

    protected async Task FollowupWithSearchResultErrorAsync<T>(Services.Modrinth.SearchResult<T> status)
    {
        if (status.Success)
        {
            throw new ArgumentException("SearchResult was successful, but was expected to be an error");
        }

        string title = LocalizationService.Get("modrinth-search-unsuccessful", CommandCultureInfo);
        if (status.SearchStatus != Services.Modrinth.SearchStatus.NoResult)
            title += ". " + LocalizationService.Get("error-try-again-later", CommandCultureInfo);

        var embed = CommonEmbedBuilder.GetErrorEmbedBuilder(title, SearchErrorDescription(status)).Build();

        await FollowupAsync(embeds: new[] {embed});
    }

    /// <summary>
    ///     Why the search was unsuccessful
    /// </summary>
    protected string SearchErrorDescription<T>(Services.Modrinth.SearchResult<T> status)
    {
        return status.SearchStatus switch
        {
            Services.Modrinth.SearchStatus.ApiDown =>
                LocalizationService.Get("error-modrinth-api-unavailable", CommandCultureInfo),
            Services.Modrinth.SearchStatus.NoResult =>
                LocalizationService.Get("modrinth-search-no-result", CommandCultureInfo, new { query = status.Query }),
            Services.Modrinth.SearchStatus.UnknownError => LocalizationService.Get("error-unknown", CommandCultureInfo),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
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
        _cultureProvider.Invalidate(Context.Guild.Id);
        CommandCultureInfo = await _cultureProvider.GetCultureAsync(Context.Interaction);
    }

    public override async Task BeforeExecuteAsync(ICommandInfo command)
    {
        CommandCultureInfo = await _cultureProvider.GetCultureAsync(Context.Interaction);
        await base.BeforeExecuteAsync(command);
    }
}