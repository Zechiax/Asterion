using Asterion.Common;
using Asterion.Interfaces;
using Discord.Interactions;
using Discord.WebSocket;

namespace Asterion.Modules;

public class BotCommands : AsterionInteractionModuleBase
{
#if DEBUG
    [SlashCommand("ping", "Pings the bot", runMode: RunMode.Async)]
    public async Task Ping()
    {
        await RespondAsync($"Pong! Latency: {Context.Client.Latency}ms");
    }
#endif
}