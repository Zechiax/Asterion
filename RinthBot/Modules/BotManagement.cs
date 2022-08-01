﻿using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using RinthBot.Attributes;
using RinthBot.Interfaces;
using RinthBot.Services;

namespace RinthBot.Modules;

[DoOwnerCheck]
public class BotManagement: InteractionModuleBase<SocketInteractionContext>
{
    private readonly IDataService _dataService;

    public BotManagement(IServiceProvider serviceProvider)
    {
        _dataService = serviceProvider.GetRequiredService<DataService>();
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
#endif
    
}