using System.Runtime.CompilerServices;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RinthBot.Attributes;
using RinthBot.ComponentBuilders;
using RinthBot.EmbedBuilders;
using RinthBot.Interfaces;

namespace RinthBot.Modules;

public class SettingsModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<SettingsModule> _logger;
    private readonly IDataService _dataService;

    public SettingsModule(IServiceProvider serviceProvider)
    {
        _logger = serviceProvider.GetRequiredService<ILogger<SettingsModule>>();
        _dataService = serviceProvider.GetRequiredService<IDataService>();
    }

    [SlashCommand("settings", "Change settings for RinthBot")]
    public async Task SettingsCommand()
    {
        var embed = SettingsEmbedBuilder.GetIntroEmbedBuilder();
        var components = SettingsComponentBuilder.GetIntroButtons(Context.User.Id.ToString());

        await RespondAsync(embed: embed.Build(), components: components.Build());
    }
}

public class SettingsInteractionModule : InteractionModuleBase
{
    private readonly ILogger<SettingsInteractionModule> _logger;
    private readonly IDataService _dataService;
    public SettingsInteractionModule(ILogger<SettingsInteractionModule> logger, IDataService dataService)
    {
        _logger = logger;
        _dataService = dataService;
    }

    [DoUserCheck]
    [RequireUserPermission(GuildPermission.Administrator)]
    [ComponentInteraction(SettingsComponentBuilder.MoreButtonId, runMode: RunMode.Async)]
    public async Task SettingsMore()
    {
        await DeferAsync();
        
        var guild = await _dataService.GetGuildByIdAsync(Context.Guild.Id);

        if (guild is null)
        {
            await FollowupAsync("Something went wrong, please try again later", ephemeral: true);
            return;
        }

        var embed = SettingsEmbedBuilder.GetMoreSettingsEmbedBuilder(guild);
        var component =
            SettingsComponentBuilder.GetMoreSettingsComponents(Context.User.Id.ToString(), guild.GuildSettings);

        await ModifyOriginalResponseAsync(x =>
        {
            x.Embed = embed.Build();
            x.Components = component.Build();
        });
    }

    [DoUserCheck]
    [RequireUserPermission(GuildPermission.Administrator)]
    [ComponentInteraction("settings-message-scan:*;*", runMode: RunMode.Async)]
    public async Task ScanMessageSet(string userId, bool scanMessageStatus)
    {
        await DeferAsync();
        
        var guild = await _dataService.GetGuildByIdAsync(Context.Guild.Id);
        
        if (guild is null)
        {
            await FollowupAsync("Something went wrong, please try again later", ephemeral: true);
            return;
        }

        guild.GuildSettings.CheckMessagesForModrinthLink = !scanMessageStatus;

        var success = await _dataService.UpdateGuildAsync(guild);

        if (success)
        {
            var component =
                SettingsComponentBuilder.GetMoreSettingsComponents(Context.User.Id.ToString(), guild.GuildSettings);
            await ModifyOriginalResponseAsync(x => x.Components = component.Build());
            return;
        }
        else
        {
            await FollowupAsync("Something went wrong, please try again later", ephemeral: true);
            return;
        }
    }

    [DoUserCheck]
    [RequireUserPermission(GuildPermission.Administrator)]
    [ComponentInteraction(SettingsComponentBuilder.NotificationButtonId, runMode: RunMode.Async)]
    public async Task ChangeViewSettings()
    {
        await FollowupAsync("TBD, in the meantime, please use **/message-style** command'", ephemeral: true);
    }
}