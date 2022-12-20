using Discord.Interactions;

namespace Asterion.Modules;

public class BotCommands : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("ping", "Pings the bot", runMode: RunMode.Async)]
    public async Task Ping()
    {
        await RespondAsync($"Pong :ping_pong: It took me {Context.Client.Latency}ms to respond to you", ephemeral: true);
    }
}