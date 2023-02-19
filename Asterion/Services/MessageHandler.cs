using System.Reflection;
using Asterion.Interfaces;
using Asterion.Services.Modrinth;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Asterion.Services;

public class MessageHandler
{
    private readonly IDataService _dataService;
    private readonly CommandService _commands;
    private readonly ILogger<MessageHandler> _logger;
    private readonly IServiceProvider _services;
    private readonly DiscordSocketClient _client;
    private readonly ModrinthHelper _helper;

    public MessageHandler(IServiceProvider services, ILogger<MessageHandler> logger)
    {
        _services = services;
        _logger = logger;

        _helper = new ModrinthHelper();
        _dataService = services.GetRequiredService<IDataService>();
        _client = services.GetRequiredService<DiscordSocketClient>();
        _commands = services.GetRequiredService<CommandService>();

        _client.MessageReceived += MessageReceivedAsync;
    }
    
    public async Task InitializeAsync()
    {
        // register modules that are public and inherit ModuleBase<T>.
        await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
    }


    private async Task MessageReceivedAsync(SocketMessage rawMessage)
    {
        // Ignore system messages, or messages from other bots
        if (rawMessage is not SocketUserMessage {Source: MessageSource.User} message)
            return;

        if (rawMessage.Channel is not IGuildChannel guildChannel)
        {
            return;
        }

        var guild = await _dataService.GetGuildByIdAsync(guildChannel.GuildId);

        if (guild?.GuildSettings.CheckMessagesForModrinthLink == false)
        {
            _logger.LogDebug("Guild ID {GuildId} has disabled checking for Modrinth message", guild.GuildId);
            return;
        }

        var context = new SocketCommandContext(_client, message);
        
        if (_helper.TryParseProjectSlugOrId(rawMessage.CleanContent, out var slugOrId))
        {
            _logger.LogDebug("Found Modrinth link, message id: {MessageId}; parsed ID: {ProjectId}", rawMessage.Id, slugOrId);
            
            await _commands.ExecuteAsync(context, $"project {slugOrId}", _services);
            return;
        }
        
        _logger.LogDebug("Not Modrinth link");
    }
}