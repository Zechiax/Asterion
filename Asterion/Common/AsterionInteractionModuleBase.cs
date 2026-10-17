using System.Globalization;
using Asterion.EmbedBuilders;
using Asterion.Interfaces;
using Discord;
using Discord.Interactions;
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

        string? description;
        switch (status.SearchStatus)
        {
            case Services.Modrinth.SearchStatus.ApiDown:
                description = LocalizationService.Get("error-modrinth-api-unavailable", CommandCultureInfo);
                title += ". " + LocalizationService.Get("error-try-again-later", CommandCultureInfo);
                break;
            case Services.Modrinth.SearchStatus.NoResult:
                description = LocalizationService.Get("modrinth-search-no-result", CommandCultureInfo, new { query = status.Query });
                break;
            case Services.Modrinth.SearchStatus.UnknownError:
                description = LocalizationService.Get("error-unknown", CommandCultureInfo);
                title += ". " + LocalizationService.Get("error-try-again-later", CommandCultureInfo);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        var embed = CommonEmbedBuilder.GetErrorEmbedBuilder(title, description).Build();

        await FollowupAsync(embeds: new[] {embed});
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