using Asterion.AutocompleteHandlers;
using Asterion.Common;
using Asterion.Interfaces;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace Asterion.Modules;

[RequireOwner]
public class BotManagement : AsterionInteractionModuleBase
{
    private readonly IDataService _dataService;

    public BotManagement(IServiceProvider serviceProvider, ILocalizationService localizationService) : base(localizationService)
    {
        _dataService = serviceProvider.GetRequiredService<IDataService>();
    }

#if DEBUG
    [SlashCommand("register", "Registers this guild to the bot", runMode: RunMode.Async)]
    public async Task Register()
    {
        await RespondAsync("Registering this guild", ephemeral: true);
        await _dataService.AddGuildAsync(Context.Guild.Id);
        await FollowupAsync("Registered", ephemeral: true);
    }

    [SlashCommand("unregister", "Un-registers this guild to the bot", runMode: RunMode.Async)]
    public async Task Unregister()
    {
        await RespondAsync("Unregistering this guild", ephemeral: true);
        await _dataService.RemoveGuildAsync(Context.Guild.Id);
        await FollowupAsync("Unregistered", ephemeral: true);
    }

    [SlashCommand("test", "general testing command")]
    public async Task Test([Autocomplete(typeof(GameVersionAutocompletionHandler))] string gameVersion)
    {
        await RespondAsync(gameVersion, ephemeral: true);
    }
#endif
}