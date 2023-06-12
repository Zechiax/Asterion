using Asterion.Common;
using Discord.Interactions;
using Discord.WebSocket;

namespace Asterion.Modules;

public class BotCommands : AsterionInteractionModuleBase
{
#if DEBUG
    [SlashCommand("ping", "Pings the bot", runMode: RunMode.Async)]
    public async Task Ping()
    {
        if (Context.Client is BaseSocketClient socketClient)
        {
            var latency = socketClient.Latency;
            await RespondAsync($"Pong! Latency: {latency}ms");
        }
        else
        {
            await RespondAsync("Unable to determine latency.");
        }
    }
#endif
}