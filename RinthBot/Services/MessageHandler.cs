using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RinthBot.Interfaces;

namespace RinthBot.Services;

public class MessageHandler
{
    private readonly IDataService _dataService;
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactionService;

    public MessageHandler(IServiceProvider services)
    {
        _dataService = services.GetRequiredService<IDataService>();
        _client = services.GetRequiredService<DiscordSocketClient>();
        _interactionService = services.GetRequiredService<InteractionService>();
    }
}