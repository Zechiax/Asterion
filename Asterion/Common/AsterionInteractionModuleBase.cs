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

    public override async Task BeforeExecuteAsync(ICommandInfo command)
    {
        CommandCultureInfo = await _cultureProvider.GetCultureAsync(Context.Interaction);
        await base.BeforeExecuteAsync(command);
    }
}