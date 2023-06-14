using System.Globalization;
using Asterion.Interfaces;
using Discord;
using Discord.Interactions;
using Color = Discord.Color;

namespace Asterion.Common;

public class AsterionInteractionModuleBase : InteractionModuleBase<SocketInteractionContext>
{
    protected CultureInfo? CommandCultureInfo { get; set; }
    protected ILocalizationService LocalizationService { get; } 
    public AsterionInteractionModuleBase(ILocalizationService localizationService)
    {
        LocalizationService = localizationService;
    }

    protected async Task FollowupWithSearchResultErrorAsync<T>(Services.Modrinth.SearchResult<T> status) 
    {
        if (status.Success)
        {
            throw new ArgumentException("SearchResult was successful, but was expected to be an error");
        }
        
        string title = LocalizationService.Get("Modrinth_Search_Unsuccessful", CommandCultureInfo);

        string? description;
        switch (status.SearchStatus)
        {
            case Services.Modrinth.SearchStatus.ApiDown:
                description = LocalizationService.Get("Error_ModrinthApiUnavailable", CommandCultureInfo);
                title += ". " + LocalizationService.Get("Error_TryAgainLater", CommandCultureInfo);
                break;
            case Services.Modrinth.SearchStatus.NoResult:
                description = LocalizationService.Get("Modrinth_Search_NoResult_WithQuery", CommandCultureInfo, new object[] {status.Query});
                break;
            case Services.Modrinth.SearchStatus.UnknownError:
                description = LocalizationService.Get("Error_Unknown", CommandCultureInfo);
                title += ". " + LocalizationService.Get("Error_TryAgainLater", CommandCultureInfo);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        var embed = new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(Color.Red)
            .WithCurrentTimestamp()
            .Build();
        
        await FollowupAsync(embeds: new[] {embed});
    }

    public override void BeforeExecute(ICommandInfo cmd)
    {
        // We currently set US culture for all commands
        CommandCultureInfo = CultureInfo.GetCultureInfo("en-US");
        
        base.BeforeExecute(cmd);
    }
}