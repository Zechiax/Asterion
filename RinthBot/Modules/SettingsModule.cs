using System.Runtime.CompilerServices;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RinthBot.Attributes;
using RinthBot.ComponentBuilders;
using RinthBot.Database.Models;
using RinthBot.EmbedBuilders;
using RinthBot.Interfaces;
using RinthBot.Services;

namespace RinthBot.Modules;

[RequireContext(ContextType.Guild)]
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
        await RespondAsync(embed: SettingsEmbedBuilder.GetIntroEmbedBuilder().Build(),
            components: SettingsComponentBuilder.GetIntroButtons(Context.User.Id.ToString()).Build());
    }
}

[RequireUserPermission(GuildPermission.Administrator)]
[RequireContext(ContextType.Guild)]
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
    [ComponentInteraction(SettingsComponentBuilder.MainScreenButtonId)]
    public async Task MainSettingsScreen(ulong userId)
    {
        await DeferAsync();
        
        await ModifyOriginalResponseAsync(x =>
        {
            x.Embed = SettingsEmbedBuilder.GetIntroEmbedBuilder().Build();
            x.Components = SettingsComponentBuilder.GetIntroButtons(Context.User.Id.ToString()).Build();
        });
    }

    [DoUserCheck]
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
            await ModifyOriginalResponseAsync(x =>
            {
                x.Embed = SettingsEmbedBuilder.GetMoreSettingsEmbedBuilder(guild).Build();
                x.Components = component.Build();   
            });
            return;
        }

        await FollowupAsync("Something went wrong, please try again later", ephemeral: true);
    }

    [DoUserCheck]
    [ComponentInteraction(SettingsComponentBuilder.NotificationButtonId, runMode: RunMode.Async)]
    public async Task ChangeViewSettings()
    {
        await DeferAsync();

        var guild = await _dataService.GetGuildByIdAsync(Context.Guild.Id);

        if (guild is null)
        {
            await FollowupAsync("Something went wrong, please try again later", ephemeral: true);
            return;
        }

        var embed = SettingsEmbedBuilder.GetViewSettingsEmbed(guild);

        await ModifyOriginalResponseAsync(x =>
        {
            x.Embed = embed.Build();
            x.Components = SettingsComponentBuilder.GetMessageStyleSelectionComponents(guild, Context.User.Id.ToString())
                .Build();
        });
    }

    [DoUserCheck]
    [ComponentInteraction(SettingsComponentBuilder.ChangeMessageStyleSelectionId)]
    public async Task ChangeMessageStyle(string userId, string[] selectedStyle)
    {
        await DeferAsync();
        var style = (MessageStyle) Enum.Parse(typeof(MessageStyle), selectedStyle.First());

        var guild = await _dataService.GetGuildByIdAsync(Context.Guild.Id);

        if (guild is null)
        {
            await FollowupAsync("Something went wrong, please try again later", ephemeral: true);
            return;
        }

        guild.GuildSettings.MessageStyle = style;

        var success = await _dataService.UpdateGuildAsync(guild);

        if (!success)
        {
            await FollowupAsync("Something went wrong, please try again later", ephemeral: true);
            return;
        }
        
        var embed = SettingsEmbedBuilder.GetViewSettingsEmbed(guild);
        
        
        await ModifyOriginalResponseAsync(x =>
        {
            x.Embed = embed.Build();
            x.Components = SettingsComponentBuilder.GetMessageStyleSelectionComponents(guild, Context.User.Id.ToString())
                .Build();
        });
    }
}