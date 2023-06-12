using Asterion.Common;
using Asterion.Interfaces;
using Discord.Interactions;
using Discord.WebSocket;

namespace Asterion.Modules;

public class BotCommands : AsterionInteractionModuleBase
{
    private readonly ILocalizationService _localizationService;
    public BotCommands(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }
    
#if DEBUG
    [SlashCommand("ping", "Pings the bot", runMode: RunMode.Async)]
    public async Task Ping()
    {
        await RespondAsync($"{_localizationService.Get("HelloWorld")} Pong! Latency: {Context.Client.Latency}ms");
    }
#endif
}